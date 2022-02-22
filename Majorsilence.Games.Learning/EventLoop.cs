using System;
using System.Runtime.InteropServices;
using Majorsilence.Games.Learning.Textures;
using SDL2;

namespace Majorsilence.Games.Learning
{
    public class EventLoop
    {

        readonly Renderer _renderer;

        public EventLoop(Renderer renderer)
        {
            _renderer = renderer;
        }

        public void Start(List<PlaceholderMovingObject> movingObjects,
            List<PlaceholderStationaryObject> stationaryObjects)
        {
           

            bool Ctrl = false;
            bool Alt = false;
            bool Shift = false;
            bool quit = false;
            SDL.SDL_Event sdlEvent;
            int x = 288;
            int y = 208;

            const int SIZE_EV = 64;


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


                SDL.SDL_RenderClear(_renderer);


                foreach(var obj in stationaryObjects)
                {
                    obj.Render();
                }

                foreach (var obj in movingObjects)
                {
                    obj.Render(x, y);
                }

                //SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref dstrect);
                _renderer.Present();


            }
        }

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

    }

}

