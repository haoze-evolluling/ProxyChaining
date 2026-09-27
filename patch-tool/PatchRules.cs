using System;
using System.Collections.Generic;

namespace CvrProxyChainPatcher
{
    public class PatchChunk
    {
        public string Name { get; set; }
        public string Find { get; set; }
        public string Replace { get; set; }
    }

    public class PatchRule
    {
        public string Id { get; set; }
        public string RelPath { get; set; }
        public string Description { get; set; }
        public List<PatchChunk> Chunks { get; set; }
    }

    public static partial class PatchRules
    {
        public static List<PatchRule> GetRules()
        {
            var list = GetChainRules();
            list.AddRange(GetUpdateRules());
            return list;
        }
    }
}
