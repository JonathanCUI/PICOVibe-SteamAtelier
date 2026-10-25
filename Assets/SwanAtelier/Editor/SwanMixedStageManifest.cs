using System;
using System.IO;
using System.Xml;
using ByteDance.PICO.SpatialAdapter.Exporter.Editor;
using UnityEditor.Android;
using UnityEngine;

namespace SwanAtelier.Editor
{
    // Run after SpatialAdapter's manifest writer. Its Unconstrained preset emits
    // Automatic; the workshop explicitly needs Mixed to retain the real room.
    public sealed class SwanMixedStageManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            const string android = "http://schemas.android.com/apk/res/android";
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");
            var xml = new XmlDocument { PreserveWhitespace = true }; xml.Load(manifestPath);
            var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("android", android);
            var activity = xml.SelectSingleNode("/manifest/application/activity[@android:name='com.unity3d.player.SpatialAdapterPlayerActivity']", ns);
            var stage = activity?.SelectSingleNode("meta-data[@android:name='pico.spatial.stage.id']", ns);
            var style = activity?.SelectSingleNode("meta-data[@android:name='pico.spatial.stage.style']", ns) as XmlElement;
            if (stage == null || style == null) throw new InvalidOperationException("Swan workshop must launch directly into a Stage before applying Mixed style.");
            style.SetAttribute("value", android, ((int)SpatialAdapterManifestProcessor.StageStyle.Mixed).ToString());
            xml.Save(manifestPath);
            Debug.Log("ATELIER_SWAN_LAUNCH_STAGE_MIXED single launch container, passthrough preserved");
        }
    }
}
