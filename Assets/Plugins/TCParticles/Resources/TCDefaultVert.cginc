#if !defined(TC_DEFAULT_VERT)
#define TC_DEFALT_VERT

sampler2D _MainTex;
float4 _MainTex_ST;

TCFragment TCDefaultVert (appdata_full input) {
	TCFragment o;
	TC_DO_PARTICLE(input)
	o.pos = UnityObjectToClipPos(input.vertex);
	o.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
	o.col = input.color;
	return o;
}

#endif