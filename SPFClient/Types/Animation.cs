using GTA.Native;
using Script = GTA.Script;

namespace SPFClient.Types
{
    public class Animation
    {
        public string Dictionary { get; set; }
        public string Name { get; set; }

        public Animation()
        {
        }

        public Animation(string dict, string name)
        {
            Dictionary = dict;
            Name = name;
            Load();
        }

        public void Load()
        {
            Function.Call(Hash.REQUEST_ANIM_DICT, Dictionary);

            while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, Dictionary))
            {
                Script.Wait(0);
            }
        }
    }
}
