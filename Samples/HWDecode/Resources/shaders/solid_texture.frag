#version 330

uniform sampler2D u_Texture;

in vec2 v_TexCoord;

out vec4 v_FragColor;

void main() {
    v_FragColor = texture(u_Texture, v_TexCoord);
}