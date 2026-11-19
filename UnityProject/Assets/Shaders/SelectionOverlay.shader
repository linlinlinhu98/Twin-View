// Selection feedback only. Drawn on top of everything in the player's view camera.
// It lives on a layer that no projection camera renders, so it can never change a window.
Shader "TwinView/SelectionOverlay" {
 Properties { _Color("Color", Color) = (0.78, 0.97, 0.95, 0.85) }
 SubShader {
  Tags { "Queue"="Overlay+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Pass {
   Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   float4 vert(float4 p:POSITION):SV_POSITION { return UnityObjectToClipPos(p); }
   fixed4 frag():SV_Target { return _Color; }
   ENDCG
  }
 }
}
