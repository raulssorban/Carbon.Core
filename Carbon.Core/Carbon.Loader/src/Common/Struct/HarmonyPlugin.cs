///
/// Copyright (c) 2022 Carbon Community 
/// All rights reserved
///
using System;
using System.Collections.Generic;

namespace Carbon.Common;

internal class HarmonyPlugin : CarbonReference
{
	public string identifier;

	public List<IHarmonyModHooks> hooks
		= new List<IHarmonyModHooks>();

	public Type[] types
	{
		get
		{
			return assembly.GetTypes() ?? null;
		}
	}

	public HarmonyLib.Harmony harmonyInstance;
}