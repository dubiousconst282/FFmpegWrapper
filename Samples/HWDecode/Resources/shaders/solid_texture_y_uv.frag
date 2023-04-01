#version 330

uniform sampler2D u_TextureY;
uniform sampler2D u_TextureUV;

in vec2 v_TexCoord;

out vec4 v_FragColor;

void main() {
    vec2 texCoord = vec2(v_TexCoord.x, 1.0 - v_TexCoord.y);
    float y = texture(u_TextureY, texCoord).r;
    vec2 uv = texture(u_TextureUV, texCoord).rg - 0.5;
    float u = uv.r;
    float v = uv.g;

    //https://en.wikipedia.org/wiki/YCbCr#ITU-R_BT.709_conversion
    v_FragColor = vec4(
        y + v * 1.5748,
        y - u * 0.187324 - v * 0.468124,
        y + u * 1.8556,
        1.0);
}