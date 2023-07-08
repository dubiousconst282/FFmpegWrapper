#version 330

out vec2 v_FragCoord;

void main() {
    //Drawing a single triangle may result in slightly more efficient
    //execution due to less fragment shader invocations around the diagonals.
    //Details: https://stackoverflow.com/a/59739538
    const vec2 vertices[3] = vec2[](vec2(-1, -1), vec2(3, -1), vec2(-1, 3));

    gl_Position = vec4(vertices[gl_VertexID], 0, 1);
    v_FragCoord = gl_Position.xy * 0.5 + 0.5;
}