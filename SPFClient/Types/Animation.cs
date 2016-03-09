using GTA.Native;
using Script = GTA.Script;

namespace SPFClient.Types
{
    public class Animation
    {
        public string Dictionary { get; private set; }
        public string Name { get; private set; }

        public Animation(string dict, string name)
        {
            Dictionary = dict;
            Name = name;

            Function.Call(Hash.REQUEST_ANIM_DICT, Dictionary);

            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, Dictionary))
            {
                Script.Wait(0);
            }
        }
    }
}
