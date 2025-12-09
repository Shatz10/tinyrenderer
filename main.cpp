#include <iostream>
#include <string>
#include <limits>
#include "tgaimage.h"
#include "raster3d.h"

const TGAColor white = TGAColor(255, 255, 255, 255);
const TGAColor red   = TGAColor(255, 0,   0,   255);

// Forward declaration of line function
void line(int x0, int y0, int x1, int y1, TGAImage &image, TGAColor color);

void draw2DLines() {
    TGAImage image(100, 100, TGAImage::RGB);
    std::cout << "2D Line Rendering Mode" << std::endl;
    std::cout << "Image created: " << image.get_width() << "x" << image.get_height() << std::endl;
    
    // Draw some lines
    line(13, 20, 80, 40, image, white);
    line(20, 13, 40, 80, image, red);
    line(80, 40, 13, 20, image, red);
    
    image.flip_vertically(); // Origin at the left bottom corner of the image
    image.write_tga_file("output_2d.tga");
    std::cout << "2D rendering complete. Output saved to output_2d.tga" << std::endl;
}

void line(int x0, int y0, int x1, int y1, TGAImage &image, TGAColor color) {
    bool steep = false;
    if (std::abs(x0-x1) < std::abs(y0-y1)) {
        std::swap(x0, y0);
        std::swap(x1, y1);
        steep = true;
    }
    if (x0 > x1) {
        std::swap(x0, x1);
        std::swap(y0, y1);
    }
    int dx = x1 - x0;
    int dy = y1 - y0;
    int derror2 = std::abs(dy) * 2;
    int error2 = 0;
    int y = y0;
    for (int x = x0; x <= x1; x++) {
        if (steep) {
            image.set(y, x, color);
        } else {
            image.set(x, y, color);
        }
        error2 += derror2;
        if (error2 > dx) {
            y += (y1 > y0 ? 1 : -1);
            error2 -= dx * 2;
        }
    }
}

void showMenu() {
    std::cout << "\n=== Tiny Renderer ===" << std::endl;
    std::cout << "1. 2D Line Rendering" << std::endl;
    std::cout << "2. 3D Rasterization" << std::endl;
    std::cout << "3. Exit" << std::endl;
    std::cout << "Select an option (1-3): ";
}

int main(int argc, char** argv) {
    int choice = 0;
    
    while (true) {
        showMenu();
        std::cin >> choice;
        
        switch (choice) {
            case 1:
                draw2DLines();
                break;
            case 2:
                std::cout << "\n3D Rasterization Mode" << std::endl;
                render3DScene();
                std::cout << "3D rendering complete. Output saved to output.tga" << std::endl;
                break;
            case 3:
                std::cout << "Exiting..." << std::endl;
                return 0;
            default:
                std::cout << "Invalid option. Please try again." << std::endl;
                // Clear error flags and ignore the rest of the line
                std::cin.clear();
                std::cin.ignore(std::numeric_limits<std::streamsize>::max(), '\n');
                break;
        }
    }
    
    return 0;
}
