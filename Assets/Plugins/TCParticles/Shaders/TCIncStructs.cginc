/*
Each datatype in this included file has it's own constant name.
If that name is'nt defined prior to including this file,
the default datastructure (described below) is used.
Otherwise, it's supposed that a shader writer (user) has overridden it.
E.g., to do so for user_vert_data you need to write in your shader:
#define TC_USER_VERT_STRUCT
struct user_vert_data {
	float MyFloatVar; // your 1st variable
	int ExtraIntVar; // your 2nd variable
	float3 AdditionalPos; // your 3rd variable... etc.
};

Please note that:
1. you need to define the respective compiler constant
	 for this structure (like TC_USER_VERT_STRUCT above).
2. you also need to do it BEFORE including this file.
3. it doesn't matter where the structure itself is defined,
	 just don't forget to do it :)

So if you need to use some default structures in your custom one,
you can write a code like this:
1. define TC_USER_VERT_STRUCT
2. include this file
3. define your cusom user_vert_data
*/

struct Particle
{
	float3 pos;
	float3 velocity;
	float baseSize;
	float life;
	float deltLife;
	float rotation;
	float mass;
};



#ifndef TC_V2F_STRUCT
#define TC_V2F_STRUCT
/*
The standard vertex-to-fragment passing datatype.
However, it contains an instance of user_v2f_data, which (if overriden)
	allows to pass some custom user values from vertex to fragment shader.
*/
struct particle_fragment
{
	half4 pos : SV_POSITION;
	fixed4 col : COLOR;
	half2 uv : TEXCOORD0;

#ifdef TC_OFFSCREEN
	float4 projPos : TEXCOORD1;
#endif


#ifdef TC_USER_V2F_STRUCT
	tc_user_v2f_data custom;
#endif
};
#endif