using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA.Native;
using GTA;

namespace SPFClient.UI
{
    public delegate void UIInputHandler(UIInput sender, string result);

    public class UIInput
    {
        public event UIInputHandler ReturnedResult;

        bool updatingKeyboard = false;

        public void Update()
        {
            if (!updatingKeyboard)
            {
                return;
            }

            string result;
            UpdateOnscreenKeyboard(out result);

            if (result != null)
            {
                ReturnedResult?.Invoke(this, result);
                updatingKeyboard = false;
            }
        }

        public void Display(string initialText = "")
        {
            Function.Call(Hash.DISPLAY_ONSCREEN_KEYBOARD, false, "FMMC_KEY_TIP", "", initialText, "", "", "", 40);
            updatingKeyboard = true;
        }

        private void UpdateOnscreenKeyboard(out string result)
        {
            if (Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD) == 2)
            {
                updatingKeyboard = false;
                result = null;
                return;
            }

            while (Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD) == 0)
                Script.Wait(0);

            if (Function.Call<string>(Hash.GET_ONSCREEN_KEYBOARD_RESULT) == null)
            {
                updatingKeyboard = false;
                result = null;
                return;
            }

            result = Function.Call<string>(Hash.GET_ONSCREEN_KEYBOARD_RESULT);
            updatingKeyboard = false;
        }
    }
}
