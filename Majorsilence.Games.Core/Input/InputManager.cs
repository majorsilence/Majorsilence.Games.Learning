using System.Runtime.InteropServices;

namespace Majorsilence.Games.Core.Input;
using SDL2;

public static class InputManager
{
    private static bool[] currentKeyStates;
    private static bool[] previousKeyStates;
    private static int keyCount;

    static InputManager()
    {
        // Initialize SDL's video subsystem (required for input handling)
        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
        {
            throw new Exception("SDL could not initialize! SDL_Error: " + SDL.SDL_GetError());
        }

        // Get the number of keys
        currentKeyStates = new bool[(int)SDL.SDL_Scancode.SDL_NUM_SCANCODES];
        previousKeyStates = new bool[(int)SDL.SDL_Scancode.SDL_NUM_SCANCODES];
        keyCount = currentKeyStates.Length;
    }

    public static void Update()
    {
        // Copy current key states to previous key states
        Array.Copy(currentKeyStates, previousKeyStates, keyCount);

        // Update current key states
        // continuous-response keys
        var keyState = GetKeyboardKeyState();
        for (int i = 0; i < keyCount; i++)
        {
            currentKeyStates[i] = keyState[i] == 1;
        }

        // Handle all the SDL events
        SDL.SDL_Event e;
        while (SDL.SDL_PollEvent(out e) != 0)
        {
            if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
            {
                currentKeyStates[(int)e.key.keysym.scancode] = true;
            }
            else if (e.type == SDL.SDL_EventType.SDL_KEYUP)
            {
                currentKeyStates[(int)e.key.keysym.scancode] = false;
            }
            else if (e.type == SDL.SDL_EventType.SDL_WINDOWEVENT)
            {
                if (e.window.windowEvent == SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED)
                {
                    // Handle window resize event
                }
            }
        }
    }

    private static byte[] GetKeyboardKeyState()
    {
        // Get the current state of the keyboard
        int numKeys;
        IntPtr keyStatePtr = SDL.SDL_GetKeyboardState(out numKeys);
        byte[] keyState = new byte[numKeys];
        Marshal.Copy(keyStatePtr, keyState, 0, numKeys);
        return keyState;
    }

    public static bool IsKeyPressed(SDL.SDL_Scancode key)
    {
        return currentKeyStates[(int)key];
    }
    
    public static bool IsKeyJustPressed(SDL.SDL_Scancode key)
    {
        return currentKeyStates[(int)key] && !previousKeyStates[(int)key];
    }

    public static bool IsKeyJustReleased(SDL.SDL_Scancode key)
    {
        return !currentKeyStates[(int)key] && previousKeyStates[(int)key];
    }
    
    public static bool IsCtrlPressed()
    {
        return IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LCTRL) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RCTRL);
    }

    public static bool IsAltPressed()
    {
        return IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LALT) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RALT);
    }

    public static bool IsShiftPressed()
    {
        return IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT);
    }
    
    
    public static bool GetKey(SDL.SDL_Keycode _keycode)
    {
        // https://stackoverflow.com/questions/63808884/sdl2-cs-getkeyboardstate-intptr-to-byte-array
        int arraySize;
        var isKeyPressed = false;
        IntPtr origArray = SDL.SDL_GetKeyboardState(out arraySize);
        var keys = new byte[arraySize];
        var keycode = (byte)SDL.SDL_GetScancodeFromKey(_keycode);
        Marshal.Copy(origArray, keys, 0, arraySize);
        isKeyPressed = keys[keycode] == 1;
        return isKeyPressed;
    }
}

/*
Notes from gpt:

In the context of handling keyboard input, especially when using libraries like SDL2, it's important to understand the difference between scancodes and keycodes. Both are used to represent keys on a keyboard, but they serve different purposes and have distinct characteristics.
   
   ### Scancode
   
   A **scancode** represents the physical location of a key on the keyboard. It is hardware-specific and does not change regardless of the keyboard layout or language settings. Scancodes are useful when you want to handle keys based on their physical position on the keyboard rather than their character representation.
   
   #### Characteristics of Scancodes:
   1. **Hardware-Specific**: Directly related to the physical key on the keyboard.
   2. **Layout-Independent**: Remains the same regardless of the keyboard layout (e.g., QWERTY, AZERTY).
   3. **Consistent Position**: Useful for games where the key's physical location is more important than its character representation.
   
   Example:
   - The key located at the top left corner of the keyboard (usually `ESC`) will have the same scancode regardless of whether the keyboard layout is QWERTY, AZERTY, or any other.
   
   ### Keycode
   
   A **keycode** represents the logical symbol or character that the key produces, which can vary depending on the keyboard layout and language settings. Keycodes are useful when you want to handle input based on the actual character or function the key represents.
   
   #### Characteristics of Keycodes:
   1. **Layout-Dependent**: Changes based on the keyboard layout and language settings.
   2. **Character Representation**: Represents the actual character or function (e.g., 'A', '1', 'Enter').
   3. **Locale-Specific**: Useful for text input where the character produced by the key is important.
   
   Example:
   - The key that produces the character 'A' on a QWERTY keyboard will produce a different character on an AZERTY keyboard, and thus, the keycode will be different.
   
   ### Example in SDL2
   
   Here's an example of how you might use scancodes and keycodes in SDL2:
   
   ```csharp
   using SDL2;
   
   public static class Input
   {
       private static bool[] currentKeyStates;
       private static bool[] previousKeyStates;
       private static int keyCount;
   
       static Input()
       {
           // Initialize SDL's video subsystem (required for input handling)
           if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
           {
               throw new Exception("SDL could not initialize! SDL_Error: " + SDL.SDL_GetError());
           }
   
           // Get the number of keys
           currentKeyStates = new bool[SDL.SDL_NUM_SCANCODES];
           previousKeyStates = new bool[SDL.SDL_NUM_SCANCODES];
           keyCount = currentKeyStates.Length;
       }
   
       public static void Update()
       {
           // Copy current key states to previous key states
           Array.Copy(currentKeyStates, previousKeyStates, keyCount);
   
           // Handle all the SDL events
           SDL.SDL_Event e;
           while (SDL.SDL_PollEvent(out e) != 0)
           {
               if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
               {
                   currentKeyStates[e.key.keysym.scancode] = true;
               }
               else if (e.type == SDL.SDL_EventType.SDL_KEYUP)
               {
                   currentKeyStates[e.key.keysym.scancode] = false;
               }
           }
       }
   
       public static bool IsScancodePressed(SDL.SDL_Scancode scancode)
       {
           return currentKeyStates[(int)scancode];
       }
   
       public static bool IsKeycodePressed(SDL.SDL_Keycode keycode)
       {
           var scancode = SDL.SDL_GetScancodeFromKey(keycode);
           return currentKeyStates[(int)scancode];
       }
   }
   ```
   
   ### Usage Example
   
   ```csharp
   while (isRunning)
   {
       Input.Update();
   
       // Check if the physical key at the top left corner (usually ESC) is pressed
       if (Input.IsScancodePressed(SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE))
       {
           isRunning = false;
       }
   
       // Check if the key that produces 'A' is pressed (depends on layout)
       if (Input.IsKeycodePressed(SDL.SDL_Keycode.SDLK_a))
       {
           // Do something when 'A' is pressed
       }
   
       // Other game loop code
   }
   ```
   
   ### Summary
   
   - **Scancode**: Represents the physical key position, independent of layout (useful for consistent key mapping across different keyboard layouts).
   - **Keycode**: Represents the character or function of the key, dependent on layout (useful for handling text input or specific character functions).
   
   Understanding these differences helps you choose the right approach for handling keyboard input based on your application's needs.

*/