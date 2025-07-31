using UnityEngine;

namespace SheetSync.Tests.TestTypes
{
    [System.Serializable]
    public class HumanMaster : ScriptableObject
    {
        public int humanId;
        public string name;
        public int age;
        public HumanType humanType;
    }
}