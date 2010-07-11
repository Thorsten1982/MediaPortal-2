float4x4 worldViewProj : WORLDVIEWPROJ; // Our world view projection matrix
texture  g_texture; // Color texture
float4   g_solidcolor = float4(1.0f, 1.0f, 1.0f, 1.0f);

sampler textureSampler = sampler_state
{
  Texture = <g_texture>;
  MipFilter = LINEAR;
  MinFilter = LINEAR;
  MagFilter = LINEAR;
};
                          
// application to vertex structure
struct a2v
{
  float4 Position  : POSITION0;
  float4 Color     : COLOR0;
  float2 Texcoord  : TEXCOORD0;  // vertex texture coords 
};

// vertex shader to pixelshader structure
struct v2p
{
  float4 Position   : POSITION;
  float4 Color      : COLOR0;
  float2 Texcoord   : TEXCOORD0;
};

// pixel shader to frame
struct p2f
{
  float4 Color : COLOR0;
};

void renderVertexShader(in a2v IN, out v2p OUT)
{
  OUT.Position = mul(IN.Position, worldViewProj);
  OUT.Texcoord = IN.Texcoord;
}

void renderPixelShader(in v2p IN, out p2f OUT)
{
  half4 texPos = half4(IN.Texcoord.x, IN.Texcoord.y, 0, 1);
  OUT.Color = tex2D(textureSampler, half2(texPos.x, texPos.y));

  OUT.Color[3] *= g_solidcolor[3];
}

technique simple
{
  pass p0
  {
    VertexShader = compile vs_2_0 renderVertexShader();
    PixelShader  = compile ps_2_0 renderPixelShader();
  }
}