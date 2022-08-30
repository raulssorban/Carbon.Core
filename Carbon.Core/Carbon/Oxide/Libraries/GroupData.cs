﻿using ProtoBuf;
using System.Collections.Generic;

namespace Oxide.Core.Libraries
{
    [ProtoContract ( ImplicitFields = ImplicitFields.AllFields )]
    public class GroupData
    {
        public string Title { get; set; } = string.Empty;

        public int Rank { get; set; }

        public HashSet<string> Perms { get; set; } = new HashSet<string> ();

        public string ParentGroup { get; set; } = string.Empty;
    }
}