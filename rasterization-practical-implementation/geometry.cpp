#include "geometry.h"
#include <cmath>

Vec3f cross(const Vec3f &v1, const Vec3f &v2) {
    return Vec3f(
        v1.y * v2.z - v1.z * v2.y,
        v1.z * v2.x - v1.x * v2.z,
        v1.x * v2.y - v1.y * v2.x
    );
}

float dot(const Vec3f &v1, const Vec3f &v2) {
    return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
}

void normalize(Vec3f &v) {
    float len = std::sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    if (len > 0) {
        float invLen = 1 / len;
        v.x *= invLen;
        v.y *= invLen;
        v.z *= invLen;
    }
}

Matrix44f operator*(const Matrix44f &a, const Matrix44f &b) {
    Matrix44f res;
    for (uint8_t i = 0; i < 4; ++i) {
        for (uint8_t j = 0; j < 4; ++j) {
            res[i][j] = a[i][0] * b[0][j] + a[i][1] * b[1][j] + 
                        a[i][2] * b[2][j] + a[i][3] * b[3][j];
        }
    }
    return res;
}
