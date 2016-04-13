using System;
using System.Windows.Forms;
using GTA;
using GTA.Native;
using Control = System.Windows.Forms.Control;

namespace SPFClient.UI
{
    public delegate void UIChatMessageHandler(UIChat sender, string message);

    public class UIChat : GTA.Script
    {
        static int cHandle;
        static string username;
        static bool active;
        static bool capsLock;
        bool exitToggle;

        public static bool Active { get { return active; } }

        private static DateTime displayTimer = new DateTime();

        public static event UIChatMessageHandler MessageSent;

        private string textBuffer = "";

        public UIChat()
        {
            Tick += OnTick;
            KeyDown += KeyPressed;
            KeyUp += KeyReleased;
            cHandle = Function.Call<int>(Hash.REQUEST_SCALEFORM_MOVIE, "multiplayer_chat");
            username = Game.Player.Name;
            active = false;
        }

        private void KeyReleased(object sender, KeyEventArgs e)
        {
            if (active && e.KeyCode == Keys.CapsLock)
                capsLock = Control.IsKeyLocked(Keys.CapsLock);
        }

        private void KeyPressed(object sender, KeyEventArgs e)
        {
            if (!active)
                return;

            var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)e.KeyCode);

            char keyChar = KeyInterop.GetCharFromKey(key, e.Shift);

            if ((e.Modifiers & Keys.Shift) != 0)
            {
                switch (keyChar)
                {
                    case ',': keyChar = '<'; break;
                    case '.': keyChar = '>'; break;
                    case '/': keyChar = '?'; break;
                    case ';': keyChar = ':'; break;
                    case '\'': keyChar = '"'; break;
                    case '\\': keyChar = '|'; break;
                    case '[': keyChar = '{'; break;
                    case ']': keyChar = '}'; break;
                    case '1': keyChar = '!'; break;
                    case '2': keyChar = '@'; break;
                    case '3': keyChar = '#'; break;
                    case '4': keyChar = '$'; break;
                    case '5': keyChar = '%'; break;
                    case '6': keyChar = '^'; break;
                    case '7': keyChar = '&'; break;
                    case '8': keyChar = '*'; break;
                    case '9': keyChar = '('; break;
                    case '0': keyChar = ')'; break;
                    case '-': keyChar = '_'; break;
                    case '=': keyChar = '+'; break;
                    case '`': keyChar = '~'; break;
                    default: keyChar = char.ToUpper(keyChar); break;
                }
            }

            else if (char.IsLetterOrDigit(keyChar))
            {
                if (capsLock)
                    keyChar = char.ToUpper(keyChar);
            }

            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Space:
                        AddTypingText(' ');
                        textBuffer += ' ';
                        return;

                    case Keys.Back:
                        if (textBuffer.Length < 1)
                        {
                            if (exitToggle)
                            {
                                SetTypingDone();
                                SetVisibleState(VisibleState.Visible);
                                ResetDisplayTimer(5000);
                                exitToggle = false;
                            }
                            else exitToggle = true;
                        }
                        else
                        {
                            SetVisibleState(VisibleState.Typing, TypeMode.None);
                            textBuffer = textBuffer.Substring(0, textBuffer.Length - 1);
                            AddTypingText(textBuffer);
                        }
                        return;

                    case Keys.Enter:
                        OnMessageSent(textBuffer);
                        SetTypingDone(true);
                        SetVisibleState(VisibleState.Visible);
                        ResetDisplayTimer(5000);
                        return;

                    case Keys.Escape:
                        SetTypingDone();
                        SetVisibleState(VisibleState.Visible);
                        ResetDisplayTimer(5000);
                        return;
                }
            }

            if (keyChar != ' ')
            {
                AddTypingText(keyChar);
                textBuffer += keyChar;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (displayTimer.Ticks > 0 && DateTime.Now > displayTimer)
            {
                SetVisibleState(VisibleState.Hidden);
                displayTimer = new DateTime();
            }

            if (active)
            {
                Function.Call(Hash.DISABLE_ALL_CONTROL_ACTIONS, 1);

                if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)GTA.Control.CursorScrollUp))
                {
                    Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION, cHandle, "PAGE_UP");
                    Function.Call(Hash._POP_SCALEFORM_MOVIE_FUNCTION_VOID);
                }

                else if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 0, (int)GTA.Control.CursorScrollDown))
                {
                    Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION, cHandle, "PAGE_DOWN");
                    Function.Call(Hash._POP_SCALEFORM_MOVIE_FUNCTION_VOID);
                }
            }

            Function.Call((Hash)0x0DF606929C105BE1, cHandle, 255, 255, 255, 100, 0);
        }

        static void ResetDisplayTimer(int interval)
        {
            displayTimer = DateTime.Now + TimeSpan.FromMilliseconds(interval);
        }

        protected void SetTypingDone(bool addMessage = false)
        {
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION, cHandle, "SET_TYPING_DONE");
            Function.Call(Hash._POP_SCALEFORM_MOVIE_FUNCTION_VOID);

            if (addMessage)
            {
                AddFeedMessage(username, textBuffer);
            }

            textBuffer = string.Empty;
        }


        protected void AddTypingText(string text)
        {
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION, cHandle, "ADD_TEXT");
            Function.Call(Hash._BEGIN_TEXT_COMPONENT, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, text);
            Function.Call(Hash._END_TEXT_COMPONENT);
            Function.Call(Hash._POP_SCALEFORM_MOVIE_FUNCTION_VOID);
        }

        protected void AddTypingText(char text)
        {
            if (exitToggle) exitToggle = false;
            AddTypingText(Convert.ToString(text));
        }

        public static void SetVisibleState(VisibleState state, TypeMode mode = TypeMode.None)
        {
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION, cHandle, "SET_FOCUS");
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION_PARAMETER_INT, (int)state);
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION_PARAMETER_INT, (int)mode);
            Function.Call(Hash._BEGIN_TEXT_COMPONENT, "STRING");
            Function.Call(Hash._ADD_TEXT_COMPONENT_STRING, username);
            Function.Call(Hash._END_TEXT_COMPONENT);
            Function.Call(Hash._POP_SCALEFORM_MOVIE_FUNCTION_VOID);
            active = (state == VisibleState.Typing);
            if (active) displayTimer = new DateTime();
        }

        public static void AddFeedMessage(string username, string message)
        {
            string name = username.Length < 0 ? "undefined" : username;
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION, cHandle, "ADD_MESSAGE");
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION_PARAMETER_STRING, name);
            Function.Call(Hash._PUSH_SCALEFORM_MOVIE_FUNCTION_PARAMETER_STRING, message);
            Function.Call(Hash._POP_SCALEFORM_MOVIE_FUNCTION_VOID);
            SetVisibleState(VisibleState.Visible);
            if (!active) ResetDisplayTimer(5000);
        }

        protected virtual void OnMessageSent(string msg)
        {
            MessageSent?.Invoke(this, msg);
        }

        public static void SetLocalUsername(string Username)
        {
            username = Username;
        }

        public enum VisibleState
        {
            Hidden,
            Visible,
            Typing
        }

        public enum TypeMode
        {
            None,
            Team,
            All,
            Clan
        }
    }
}
