using System;
using System.Globalization;
using UnityEngine;
namespace GansuuHead
{
    // IMGUI consumes synchronous cached text. A future Unity Localization adapter can
    // preload String Tables, override Format for Smart Strings, then notify this view.
    public abstract class PrototypeTextSource : ScriptableObject
    {
        public int Revision {get;private set;}
        public abstract string Get(string key);
        public virtual string Format(string key,params object[] arguments)
        {
            string template=Get(key);
            try{return string.Format(CultureInfo.InvariantCulture,template,arguments);}
            catch(FormatException){return template;}
        }
        protected void NotifyChanged(){unchecked{Revision++;}}
    }
}
