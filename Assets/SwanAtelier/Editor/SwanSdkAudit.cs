using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text;
using ByteDance.PICO.SpatialAdapter;
using UnityEngine;

namespace SwanAtelier.Editor
{
    public static class SwanSdkAudit
    {
        public static void Run()
        {
            var assembly=typeof(SpatialCamera).Assembly;
            var text=new StringBuilder();
            foreach(var type in assembly.GetExportedTypes().OrderBy(t=>t.FullName)) {
                text.AppendLine("TYPE "+type.FullName);
                foreach(var member in type.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
                    text.AppendLine("  "+member.MemberType+" "+member);
            }
            Directory.CreateDirectory("Builds/Evidence"); File.WriteAllText("Builds/Evidence/spatialadapter-public-api.txt",text.ToString());
            Debug.Log("ATELIER_SDK_PUBLIC_API_SAVED");
        }
    }
}
