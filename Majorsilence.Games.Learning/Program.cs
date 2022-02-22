using System.Runtime.InteropServices;
using SDL2;
using Majorsilence.Games.Learning;
using Majorsilence.Games.Learning.Surfaces;

bool quit = false;
SDL.SDL_Event sdlEvent;

SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
SDL_ttf.TTF_Init();

//var screen = SDL.SDL_CreateWindow("My SDL Empty Window",
//    SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 640, 480, 0);


var window = SDL.SDL_CreateWindow("SDL2 Displaying Image",
    SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 640, 480,
    SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);

using var renderer = new Renderer(window);

//var image = new Majorsilence.Games.Learning.Image("/Users/petergill/Downloads/stick_people.png");

using var image = new ImageSDL("/Users/petergill/Downloads/spaceship.png");

using var texture = new Majorsilence.Games.Learning.Texture(renderer, image);

using var font = new Fonts("assets/fonts/Gidole-Regular.ttf", 25);
var color = new SDL2.SDL.SDL_Color { a = 0, b = 155, g = 155, r = 150 };
using var text = new Text(font, color, "Hello World");
using var textTexture = new Texture(renderer, text);

SDL.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);

int x = 288;
int y = 208;

const int SIZE_EV = 64;

bool Ctrl = false;
bool Alt = false;
bool Shift = false;


//SDL.SDL_Event[] es = new SDL.SDL_Event[SIZE_EV];
while (!quit)
{


    // main game loop

    // see https://github.com/libsdl-org/SDL/issues/4376#issuecomment-841654449
    // event loop is done this way because SDL_PollEvent was extremely slow
    // when developing on mac book air

    //SDL.SDL_WaitEvent(out sdlEvent);


    int numkey;
    var keystate = SDL2.SDL.SDL_GetKeyboardState(out numkey);
    //var sur = Marshal.PtrToStructure<SDL.key>(_surface);


    //continuous-response keys
    if (GetKey(SDL.SDL_Keycode.SDLK_LEFT))
    {
        x = x - 2;
    }
    if (GetKey(SDL.SDL_Keycode.SDLK_RIGHT))
    {
        x = x + 2;
    }
    if (GetKey(SDL.SDL_Keycode.SDLK_UP))
    {
        y = y - 2;
    }
    if (GetKey(SDL.SDL_Keycode.SDLK_DOWN))
    {
        y = y + 2;
    }

    //single-hit keys, mouse, and other general SDL events (eg. windowing)

    while (SDL.SDL_PollEvent(out sdlEvent) == 1)
    {
        switch (sdlEvent.type)
        {

            case SDL.SDL_EventType.SDL_QUIT:
                quit = true;
                break;
                /*
            case SDL.SDL_EventType.SDL_KEYDOWN:
                SDL.SDL_Keycode kc = sdlEvent.key.keysym.sym;

                if ((kc == SDL.SDL_Keycode.SDLK_LCTRL) || (kc == SDL.SDL_Keycode.SDLK_RCTRL))
                    Ctrl = true;
                if ((kc == SDL.SDL_Keycode.SDLK_LALT) || (kc == SDL.SDL_Keycode.SDLK_RALT))
                    Alt = true;
                if ((kc == SDL.SDL_Keycode.SDLK_LSHIFT) || (kc == SDL.SDL_Keycode.SDLK_RSHIFT))
                    Shift = true;


                
                switch (kc)
                {
                    case SDL.SDL_Keycode.SDLK_LEFT: x = x - 1; break;
                    case SDL.SDL_Keycode.SDLK_RIGHT: x = x + 1; break;
                    case SDL.SDL_Keycode.SDLK_UP: y = y - 1; break;
                    case SDL.SDL_Keycode.SDLK_DOWN: y = y + 1; break;
                }
                
                break;
                */
        }


    }

    //SDL.SDL_Rect dstrect = new SDL.SDL_Rect { x= 5, y = 5, w = 320, h= 240 };
    //SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref dstrect);
    //SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
    //SDL.SDL_RenderPresent(renderer);

    //SDL.SDL_Rect dstrect = new SDL.SDL_Rect { x = x, y = y, w = 64, h = 64 };


    SDL.SDL_RenderClear(renderer);


    textTexture.Render(0, 0);
    texture.Render(x, y);


    //SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref dstrect);
    renderer.Present();
    

}


SDL.SDL_DestroyWindow(window);

//SDL_ttf.TTF_Quit();
SDL.SDL_Quit();

bool GetKey(SDL.SDL_Keycode _keycode)
{
    // https://stackoverflow.com/questions/63808884/sdl2-cs-getkeyboardstate-intptr-to-byte-array
    int arraySize;
    bool isKeyPressed = false;
    IntPtr origArray = SDL.SDL_GetKeyboardState(out arraySize);
    byte[] keys = new byte[arraySize];
    byte keycode = (byte)SDL.SDL_GetScancodeFromKey(_keycode);
    Marshal.Copy(origArray, keys, 0, arraySize);
    isKeyPressed = keys[keycode] == 1;
    return isKeyPressed;
}