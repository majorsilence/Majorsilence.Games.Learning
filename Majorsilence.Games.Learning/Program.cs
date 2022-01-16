using System.Runtime.InteropServices;
using SDL2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using Majorsilence.Games.Learning;

bool quit = false;
SDL.SDL_Event sdlEvent;

SDL.SDL_Init(SDL.SDL_INIT_VIDEO);


//var screen = SDL.SDL_CreateWindow("My SDL Empty Window",
//    SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 640, 480, 0);


var window = SDL.SDL_CreateWindow("SDL2 Displaying Image",
    SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 640, 480, 0);

var renderer = new Renderer(window);

var image = new Majorsilence.Games.Learning.Image("/Users/petergill/Downloads/stick_people.png");
var texture =  new Majorsilence.Games.Learning.Texture(renderer, image);

while (!quit)
{
    SDL.SDL_WaitEvent(out sdlEvent);

    switch (sdlEvent.type)
    {
        case SDL.SDL_EventType.SDL_QUIT:
            quit = true;
            break;
    }

    //SDL.SDL_Rect dstrect = new SDL.SDL_Rect { x= 5, y = 5, w = 320, h= 240 };
    //SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref dstrect);
    SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
    SDL.SDL_RenderPresent(renderer);
    renderer.SaveScreenshot();
}



texture?.Dispose();

image?.Dispose();
renderer?.Dispose();
SDL.SDL_DestroyWindow(window);

SDL.SDL_Quit();
