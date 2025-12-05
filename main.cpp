#include <iostream>
#include "tgaimage.h"

const TGAColor white = TGAColor(255, 255, 255, 255);
const TGAColor red   = TGAColor(255, 0,   0,   255);

int main(int argc, char** argv) {
    TGAImage image(100, 100, TGAImage::RGB);
    std::cout << "Image created: " << image.get_width() << "x" << image.get_height() << std::endl;

    for (int x = 0; x < 100; x++) {
        for (int y = 0; y < 100; y++) {
            image.set(x, y, red);
        }
    }
    
//    image.set(52, 41, red);
//    std::cout << "Pixel set at (52, 41)" << std::endl;

    image.flip_vertically();
    std::cout << "Image flipped vertically" << std::endl;

    image.write_tga_file("output.tga");
    std::cout << "File written" << std::endl;
	return 0;
}

