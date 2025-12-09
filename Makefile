SYSCONF_LINK = g++
CPPFLAGS     = -g -I. -I./rasterization-practical-implementation
LDFLAGS      =
LIBS         = -lm

DESTDIR = ./
TARGET  = main

# Main program objects
MAIN_OBJECTS := $(patsubst %.cpp,%.o,$(wildcard *.cpp)) \
                rasterization-practical-implementation/geometry.o \
                rasterization-practical-implementation/raster3d.o

all: $(DESTDIR)$(TARGET)

# Main target
$(DESTDIR)$(TARGET): $(MAIN_OBJECTS)
	$(SYSCONF_LINK) -Wall $(LDFLAGS) -o $@ $^ $(LIBS)

# Pattern rule for .cpp files
%.o: %.cpp
	$(SYSCONF_LINK) -Wall $(CPPFLAGS) -c $(CFLAGS) $< -o $@

# Special rule for raster3d.cpp
rasterization-practical-implementation/%.o: rasterization-practical-implementation/%.cpp
	$(SYSCONF_LINK) -Wall $(CPPFLAGS) -c $(CFLAGS) $< -o $@

clean:
	-rm -f $(MAIN_OBJECTS)
	-rm -f $(TARGET)
	-rm -f *.tga

