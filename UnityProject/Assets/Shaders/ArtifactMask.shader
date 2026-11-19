Shader "Hidden/TwinView/ArtifactMask" {
 SubShader {
  Tags { "RenderType"="Opaque" }
  Pass {
   Cull Off ZWrite On ZTest LEqual
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 vert(float4 p:POSITION):SV_POSITION { return UnityObjectToClipPos(p); }
   fixed4 frag():SV_Target { return fixed4(1,1,1,1); }
   ENDCG
  }
 }
}
