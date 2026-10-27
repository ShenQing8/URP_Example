using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;

// Offline import of actual Player Profiler captures, never Editor execution timings.
[InitializeOnLoad]
public static class GrassProfilerAnalysis
{
    const string Output="Logs/MigrationPhase6-20261027/";
    [Serializable] class Request { public string id, capture; }
    static GrassProfilerAnalysis() {EditorApplication.update+=Poll;}
    static void Poll()
    {
        string path=Output+"profiler-request.json";
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(path))return;
        var request=JsonUtility.FromJson<Request>(File.ReadAllText(path));File.Delete(path);
        var report=new StringBuilder($"{DateTimeOffset.Now:O} Unity={Application.unityVersion} capture={request.capture}\n");
        if(request.capture=="state") {
            var scene=EditorSceneManager.GetActiveScene();
            report.AppendLine($"Play={EditorApplication.isPlaying} scene={scene.path} dirty={scene.isDirty} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]} pipeline={QualitySettings.renderPipeline?.name} compilationFailed={EditorUtility.scriptCompilationFailed}");
            File.WriteAllText(Output+request.id+".txt",report.ToString());return;
        }
        try {
            if(EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("C# compilation failed");
            if(!ProfilerDriver.LoadProfile(Path.GetFullPath(request.capture),false))throw new InvalidOperationException("Profiler import failed");
            int first=ProfilerDriver.firstFrameIndex,last=ProfilerDriver.lastFrameIndex;
            var threads=new HashSet<string>();var totals=new Dictionary<string,List<double>>();
            var csv=new StringBuilder("frame,thread,marker,milliseconds\n");
            for(int frame=first;frame<=last;frame++)
                for(int thread=0;thread<256;thread++) {
                    using var view=ProfilerDriver.GetRawFrameDataView(frame,thread);if(!view.valid)break;
                    threads.Add(view.threadGroupName+"/"+view.threadName);
                    var perFrame=new Dictionary<string,double>();
                    for(int sample=0;sample<view.sampleCount;sample++) {
                        string name=view.GetSampleName(sample);
                        if(name!="Grass CPU Cell Culling" && name!="RenderLoop" && name!="PlayerLoop" && name!="Gfx.WaitForPresentOnGfxThread" && name!="Gfx.PresentFrame" && name!="WaitForTargetFPS")continue;
                        string key=view.threadName+"/"+name;
                        perFrame[key]=perFrame.TryGetValue(key,out double total)?total+view.GetSampleTimeMs(sample):view.GetSampleTimeMs(sample);
                    }
                    foreach(var pair in perFrame) {
                        if(!totals.TryGetValue(pair.Key,out var values))totals[pair.Key]=values=new List<double>();values.Add(pair.Value);
                        csv.AppendLine(frame+","+pair.Key.Replace('/',',')+","+pair.Value.ToString("F9",CultureInfo.InvariantCulture));
                    }
                }
            report.AppendLine($"Profiler imported actual Player frames={last-first+1} first={first} last={last}; threads="+string.Join(",",threads.OrderBy(s=>s)));
            foreach(var pair in totals.OrderBy(p=>p.Key)) {
                var sorted=pair.Value.OrderBy(x=>x).ToArray();
                report.AppendLine($"{pair.Key} samples={sorted.Length} meanMs={sorted.Average():F6} medianMs={sorted[sorted.Length/2]:F6} p95Ms={sorted[Math.Min(sorted.Length-1,(int)(sorted.Length*.95))]:F6} maxMs={sorted.Last():F6}");
            }
            if(!totals.Keys.Any(k=>k.EndsWith("/Grass CPU Cell Culling")))throw new InvalidOperationException("Capture contains no grass CPU culling sample");
            File.WriteAllText(Output+request.id+".csv",csv.ToString());report.AppendLine("ANALYSIS PASS");
        } catch(Exception error){report.AppendLine("ANALYSIS FAIL "+error);}
        File.WriteAllText(Output+request.id+".txt",report.ToString());
    }
}
