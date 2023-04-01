#version 330

uniform sampler2D u_TextureY;
uniform sampler2D u_TextureUV;
uniform mat3 u_Yuv2RgbCoeffs;

in vec2 v_TexCoord;

out vec4 v_FragColor;

void main() {
    vec2 texCoord = vec2(v_TexCoord.x, 1.0 - v_TexCoord.y);
    float y = texture(u_TextureY, texCoord).r;
    vec2 uv = texture(u_TextureUV, texCoord).rg;
    v_FragColor = vec4(vec3(y, uv - 0.5) * u_Yuv2RgbCoeffs, 1.0);
}