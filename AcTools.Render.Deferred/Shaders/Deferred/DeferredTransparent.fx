// textures
	Texture2D gBaseMap;
	Texture2D gNormalMap;
	Texture2D gMapsMap;
	Texture2D gDepthMap;
	Texture2D gLightMap;
	Texture2D gLocalReflectionMap;
	TextureCube gReflectionCubemap;

	SamplerState samInputImage {
		Filter = MIN_MAG_LINEAR_MIP_POINT;
		AddressU = CLAMP;
		AddressV = CLAMP;
	};

	SamplerState samAnisotropic {
		Filter = ANISOTROPIC;
		MaxAnisotropy = 4;

		AddressU = WRAP;
		AddressV = WRAP;
	};
	
// input resources
	cbuffer cbPerFrame : register(b0) {
		matrix gWorldViewProjInv;
		float3 gAmbientDown;
		float3 gAmbientRange;
		float3 gEyePosW;
	}

// fn structs
	struct VS_IN {
		float3 PosL    : POSITION;
		float2 Tex     : TEXCOORD;
	};

	struct PS_IN {
		float4 PosH    : SV_POSITION;
		float2 Tex     : TEXCOORD;
	};

// functions
	float3 GetPosition(float2 uv, float depth){
		float4 position = mul(float4(uv.x * 2 - 1, -(uv.y * 2 - 1), depth, 1), gWorldViewProjInv);
		return position.xyz / position.w;
	}

	float CalculateReflectionPower(float3 toEyeNormalW, float3 normalW, float metalness){
		float rid = dot(toEyeNormalW, normalW);
		float rim = metalness + pow(1 - rid, (2 + 1 / metalness) / 3);
		return rim * 2;
	}

// one vertex shader for everything
	PS_IN vs_main(VS_IN vin) {
		PS_IN vout;
		vout.PosH = float4(vin.PosL, 1.0f);
		vout.Tex = vin.Tex;
		return vout;
	}

// debug (g-buffer)
	float4 ps_debug(PS_IN pin) : SV_Target {
		if (pin.Tex.y < 0.5){
			if (pin.Tex.x < 0.5){
				return gBaseMap.SampleLevel(samInputImage, pin.Tex * 2, 0);
			} else {
				float4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(1, 0), 0);
				if (normalValue.x == 0 && normalValue.y == 0 && normalValue.z == 0){
					return 0.0;
				}
				return 0.5 + 0.5 * normalValue;
			}
		} else {
			if (pin.Tex.x < 0.5){
				float depthValue = gDepthMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(0, 1), 0).x;
				return (1 - depthValue) * 5;
			}
		}
		
		if (pin.Tex.y < 0.75){
			if (pin.Tex.x < 0.75){
				return gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(2, 2), 0).x;
			} else {
				return gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(3, 2), 0).y;
			}
		} else {
			if (pin.Tex.x < 0.75){
				return gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(2, 3), 0).z;
			} else {
				return gMapsMap.SampleLevel(samInputImage, pin.Tex * 4 - float2(3, 3), 0).w;
			}
		}

		return 0;
	}

	technique11 Debug {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_debug() ) );
		}
	}

// debug (post effects)
	float4 ps_DebugPost(PS_IN pin) : SV_Target {
		if (pin.Tex.y < 0.5){
			if (pin.Tex.x < 0.5){
				return gBaseMap.SampleLevel(samInputImage, pin.Tex * 2, 0.0).a;
			} else {
				float2 uv = pin.Tex * 2 - float2(1.0, 0.0);

				float4 normalValue = gNormalMap.SampleLevel(samInputImage, uv, 0.0);
				float3 normal = normalValue.xyz;
		
				float depth = gDepthMap.SampleLevel(samInputImage, uv, 0.0).x;
				float3 position = GetPosition(uv, depth);
		
				float3 toEyeW = normalize(gEyePosW - position);
				float4 reflectionColor = gReflectionCubemap.Sample(samAnisotropic, reflect(-toEyeW, normal));
				return reflectionColor;
				
				float4 mapsValue = gMapsMap.SampleLevel(samInputImage, uv, 0.0);
				float glossiness = mapsValue.g;
				float reflectiveness = mapsValue.z;
				float metalness = mapsValue.w;

				return reflectionColor * reflectiveness * CalculateReflectionPower(toEyeW, normal, metalness);
			}
		} else {
			if (pin.Tex.x < 0.5){
				return gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(0.0, 1.0), 0.0);
			} else {
				return gLightMap.SampleLevel(samInputImage, pin.Tex * 2 - float2(1.0, 1.0), 0.0);
			}
		}
	}

	technique11 DebugPost {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_DebugPost() ) );
		}
	}

// debug (lighting)
	float4 ps_DebugLighting(PS_IN pin) : SV_Target {
		return gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);
	}

	technique11 DebugLighting {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_DebugLighting() ) );
		}
	}

// debug (local reflections)
	float4 ps_DebugLocalReflections(PS_IN pin) : SV_Target {
		float4 base = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float4 light = gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float4 reflection = gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		// return reflection * reflection.a;
		return (base + light) * (1 - reflection.a) + reflection * reflection.a;
	}

	technique11 DebugLocalReflections {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_DebugLocalReflections() ) );
		}
	}

// ps0
	float3 CalcAmbient(float3 normal, float3 color) {
		float up = normal.y * 0.5 + 0.5;
		float3 ambient = gAmbientDown + up * gAmbientRange;
		return ambient * color;
	}

	float3 ReflectionColor(float3 toEyeW, float3 normal, float glossiness) {
		return gReflectionCubemap.SampleBias(samAnisotropic, reflect(-toEyeW, normal), 0.3 / (glossiness + 0.3)).rgb;
	}

	float4 ps_0(PS_IN pin) : SV_Target {
		// normal and position
		float4 normalValue = gNormalMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float depthValue = gDepthMap.SampleLevel(samInputImage, pin.Tex, 0.0).x;

		float3 normal = normalValue.xyz;
		float3 position = GetPosition(pin.Tex, depthValue);

		// albedo and lightness
		float4 baseValue = gBaseMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float4 lightValue = gLightMap.SampleLevel(samInputImage, pin.Tex, 0.0);

		float3 lighted = CalcAmbient(normal, baseValue.rgb) + lightValue.rgb;

		// spec/reflection params
		float4 mapsValue = gMapsMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		float glossiness = mapsValue.g;
		float reflectiveness = mapsValue.z;
		float metalness = mapsValue.w;
		
		// reflection
		float3 toEyeW = normalize(gEyePosW - position);
		float3 reflectionColor = ReflectionColor(toEyeW, normal, glossiness);

		float4 localReflectionColor = gLocalReflectionMap.SampleLevel(samInputImage, pin.Tex, 0.0);
		reflectionColor = reflectionColor * (1 - localReflectionColor.a) + localReflectionColor.rgb * localReflectionColor.a;

		float rid = dot(toEyeW, normal);
		float rim = metalness + pow(1 - rid, 1 / metalness);

		float3 reflection = (reflectionColor - 0.5 * (metalness + 0.2) / 1.2) * saturate(reflectiveness * 
			CalculateReflectionPower(toEyeW, normal, metalness));

		// result
		return float4(lighted + reflection, 1.0);
	}

	technique11 Combine0 {
		pass P0 {
			SetVertexShader( CompileShader( vs_5_0, vs_main() ) );
			SetGeometryShader( NULL );
			SetPixelShader( CompileShader( ps_5_0, ps_0() ) );
		}
	}