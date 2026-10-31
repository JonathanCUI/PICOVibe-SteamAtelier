using System;using System.Linq;using UnityEngine;
public sealed class CanyonLightmapsV11:MonoBehaviour {
 public Texture2D[] colors,directions;public Renderer[] targets;public int[] indices;public Vector4[] scales;bool applied;
 void Awake(){Apply();}
 public void Apply(){if(applied||colors==null||colors.Length==0)return;applied=true;var maps=LightmapSettings.lightmaps.ToList();var remap=new int[colors.Length];for(int i=0;i<colors.Length;i++){int index=maps.FindIndex(m=>m.lightmapColor==colors[i]);if(index<0){index=maps.Count;maps.Add(new LightmapData{lightmapColor=colors[i],lightmapDir=directions[i]});}remap[i]=index;}LightmapSettings.lightmaps=maps.ToArray();for(int i=0;i<targets.Length;i++)if(targets[i]&&indices[i]>=0&&indices[i]<colors.Length){targets[i].lightmapIndex=remap[indices[i]];if(!targets[i].isPartOfStaticBatch)targets[i].lightmapScaleOffset=scales[i];}}
}
