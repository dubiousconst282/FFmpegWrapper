#version 330

uniform sampler2D u_TextureY;
uniform sampler2D u_TextureUV;
uniform bool u_ConvertHDRtoSDR;

in vec2 v_FragCoord;

out vec4 v_FragColor;


const mat3 Yuv2Rgb_bt709 = mat3(
    1.0,  0.0000,  1.5748,
    1.0, -0.1873, -0.4681,
    1.0,  1.8556,  0.0000
);
const mat3 Yuv2Rgb_bt2020 = mat3(
    1.0,  0.0000,  1.4746,
    1.0, -0.1645, -0.5713,
    1.0,  1.8814,  0.0000
);
const mat3 GamutFix_bt2020to709 = mat3(
     1.6605, -0.5876, -0.0728,
    -0.1246,  1.1329, -0.0083,
    -0.0182, -0.1006,  1.1187
);

//https://github.com/VoidXH/Cinema-Shader-Pack/blob/master/Shaders/HDR%20to%20SDR.hlsl
const float peakLuminance = 250.0; // Peak playback screen luminance in nits
const float knee = 0.75; // Compressor knee position
const float ratio = 1.0; // Compressor ratio: 1 = disabled, <1 = expander
const float maxCLL = 10000.0; // Maximum content light level in nits

vec3 pq2lin(vec3 pq) { // Returns luminance in nits
    const float m1inv = 16384 / 2610.0;
    const float m2inv = 32 / 2523.0;
    const float c1 = 3424 / 4096.0;
    const float c2 = 2413 / 128.0;
    const float c3 = 2392 / 128.0;

    const float gain = maxCLL / peakLuminance;
    vec3 p = pow(pq, vec3(m2inv));
    vec3 d = max(p - c1, 0) / (c2 - c3 * p);
    return pow(d, vec3(m1inv)) * gain;
}
vec3 lin2srgb(vec3 lin) {
    vec3 a = 1.055 * pow(lin, vec3(0.416667)) - 0.055;
    vec3 b = lin * 12.92;
    return mix(a, b, lessThanEqual(lin, vec3(0.0031308)));
}

float hash(vec2 co) {
    return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

void main() {
    vec2 texCoord = vec2(v_FragCoord.x, 1.0 - v_FragCoord.y);
    vec3 yuv = vec3(
        texture(u_TextureY, texCoord).r,
        texture(u_TextureUV, texCoord).rg - 0.5
    );
    vec3 col;

    if (u_ConvertHDRtoSDR) {
        col = pq2lin(yuv * Yuv2Rgb_bt2020);
        col = lin2srgb(col * GamutFix_bt2020to709);
       // col = col + (hash(v_FragCoord) - 0.5) * (0.5 / 255.0); //dither
    } else {
        col = yuv * Yuv2Rgb_bt709;
    }

    v_FragColor = vec4(col, 1.0);
}