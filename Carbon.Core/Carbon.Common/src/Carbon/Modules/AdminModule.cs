﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Carbon.Base;
using Carbon.Components;
using Carbon.Extensions;
using ConVar;
using Facepunch;
using Network;
using Oxide.Core.Libraries;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using ProtoBuf;
using UnityEngine;
using static ConsoleSystem;
using Pool = Facepunch.Pool;
using StringEx = Carbon.Extensions.StringEx;

/*
 *
 * Copyright (c) 2022-2023 Carbon Community 
 * All rights reserved.
 *
 */

namespace Carbon.Modules;

public class AdminModule : CarbonModule<AdminConfig, AdminData>
{
	public override string Name => "Admin";
	public override Type Type => typeof(AdminModule);
	public override bool EnabledByDefault => true;
	public CUI.Handler Handler { get; internal set; }

	internal float OptionWidth = 0.475f;
	internal int RangeCuts = 50;

	internal List<Tab> Tabs = new();
	internal Dictionary<BasePlayer, AdminPlayer> AdminPlayers = new();
	internal ImageDatabaseModule ImageDatabase;

	const string PanelId = "carbonmodularui";
	const string CursorPanelId = "carbonmodularuicur";

	public override void Init()
	{
		base.Init();

		Handler = new();
	}

	private void OnServerInitialized()
	{
		ImageDatabase = GetModule<ImageDatabaseModule>();

		Community.Runtime.CorePlugin.cmd.AddChatCommand(Config.OpenCommand, this, (player, cmd, args) =>
		{
			if (!CanAccess(player)) return;

			var ap = GetOrCreateAdminPlayer(player);
			ap.TabIndex = 0;

			var tab = GetTab(player);
			tab?.OnChange?.Invoke(ap, tab);

			ap.Clear();

			DrawCursorLocker(player);
			Draw(player);
		});

		Unsubscribe("OnEntityDismounted");
		Unsubscribe("CanDismountEntity");
		Unsubscribe("OnEntityVisibilityCheck");
		Unsubscribe("OnEntityDistanceCheck");

		RegisterTab(CarbonTab.Get());
		RegisterTab(EntitiesTab.Get());
		RegisterTab(PermissionsTab.Get());
		RegisterTab(PlayersTab.Get());
		RegisterTab(ModulesTab.Get());
	}

	private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
	{
		var ap = GetOrCreateAdminPlayer(player);
		if (!ap.GetStorage<bool>("wasviewingcam")) return;

		entity.Kill();
		Draw(player);

		Unsubscribe("OnEntityDismounted");
	}
	private void OnPlayerLootEnd(PlayerLoot loot)
	{
		if (EntitiesTab.LastContainerLooter != null && loot.baseEntity == EntitiesTab.LastContainerLooter.Player)
		{
			Draw(EntitiesTab.LastContainerLooter.Player);
			EntitiesTab.LastContainerLooter = null;
			Unsubscribe("OnEntityVisibilityCheck");
			Unsubscribe("OnEntityDistanceCheck");
		}
	}
	private object OnEntityDistanceCheck(BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance)
	{
		var ap = GetOrCreateAdminPlayer(player);
		var lootedEnt = ap.GetStorage<BaseEntity>("lootedent");

		if (lootedEnt == null) return null;

		return true;
	}
	private object OnEntityVisibilityCheck(BaseEntity ent, BasePlayer player, uint id, string debugName, float maximumDistance)
	{
		var ap = GetOrCreateAdminPlayer(player);
		var lootedEnt = ap.GetStorage<BaseEntity>("lootedent");

		if (lootedEnt == null) return null;

		return true;
	}

	private bool CanAccess(BasePlayer player)
	{
		var level = Config.MinimumAuthLevel;

		switch (level)
		{
			case 0:
				return true;

			case 1:
				return ServerUsers.Is(player.userID, ServerUsers.UserGroup.Moderator);

			case 2:
				return ServerUsers.Is(player.userID, ServerUsers.UserGroup.Owner);
		}

		return false;
	}

	#region Option Elements

	internal void TabButton(CUI cui, CuiElementContainer container, string parent, string text, string command, float width, float offset, bool highlight = false)
	{
		var button = cui.CreateProtectedButton(container, parent: parent, id: null,
			color: highlight ? "0.4 0.7 0.2 0.7" : "0.3 0.3 0.3 0.1",
			textColor: "1 1 1 0.5",
			text: text, 11,
			xMin: offset, xMax: offset + width, yMin: 0, yMax: 1,
			command: command,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		if (highlight)
		{
			cui.CreatePanel(container, button, null,
				color: "1 1 1 0.4",
				xMin: 0, xMax: 1f, yMin: 0f, yMax: 0.03f,
				OxMax: -0.5f);
		}
	}

	public void TabColumnPagination(CUI cui, CuiElementContainer container, string parent, int column, AdminPlayer.Page page, float height, float offset)
	{
		var id = $"{parent}{column}";

		cui.CreatePanel(container, parent, id,
			color: "0.3 0.3 0.3 0.3",
			xMin: 0.02f, xMax: 0.98f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: id, id: null,
			color: "1 1 1 0.5",
			text: $" / {page.TotalPages + 1:n0}", 9,
			xMin: 0.5f, xMax: 1f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateProtectedInputField(container, parent: id, id: null,
			color: "1 1 1 1",
			text: $"{page.CurrentPage + 1}", 9,
			xMin: 0f, xMax: 0.495f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleRight,
			command: PanelId + $".changecolumnpage {column} 4 ",
			characterLimit: 0,
			readOnly: false,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		#region Left

		cui.CreateProtectedButton(container, parent: id, id: null,
			color: page.CurrentPage > 0 ? "0.8 0.7 0.2 0.7" : "0.3 0.3 0.3 0.1",
			textColor: "1 1 1 0.5",
			text: "<<", 8,
			xMin: 0, xMax: 0.1f, yMin: 0f, yMax: 1f,
			command: page.CurrentPage > 0 ? PanelId + $".changecolumnpage {column} 2" : "",
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateProtectedButton(container, parent: id, id: null,
			color: "0.4 0.7 0.2 0.7",
			textColor: "1 1 1 0.5",
			text: "<", 8,
			xMin: 0.1f, xMax: 0.2f, yMin: 0f, yMax: 1f,
			command: PanelId + $".changecolumnpage {column} 0",
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		#endregion

		#region Right

		cui.CreateProtectedButton(container, parent: id, id: null,
			color: page.CurrentPage < page.TotalPages ? "0.8 0.7 0.2 0.7" : "0.3 0.3 0.3 0.1",
			textColor: "1 1 1 0.5",
			text: ">>", 8,
			xMin: 0.9f, xMax: 1f, yMin: 0f, yMax: 1f,
			command: page.CurrentPage < page.TotalPages ? PanelId + $".changecolumnpage {column} 3" : "",
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateProtectedButton(container, parent: id, id: null,
			color: "0.4 0.7 0.2 0.7",
			textColor: "1 1 1 0.5",
			text: ">", 8,
			xMin: 0.8f, xMax: 0.9f, yMin: 0f, yMax: 1f,
			command: PanelId + $".changecolumnpage {column} 1",
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		#endregion
	}
	public void TabPanelName(CUI cui, CuiElementContainer container, string parent, string text, float height, float offset, TextAnchor align)
	{
		cui.CreateText(container, parent: parent, id: $"{parent}text",
			color: "1 1 1 0.7",
			text: text.ToUpper(), 12,
			xMin: 0.025f, xMax: 0.98f, yMin: offset, yMax: offset + height,
			align: align,
			font: CUI.Handler.FontTypes.RobotoCondensedBold);

		cui.CreatePanel(container, $"{parent}text", null,
			color: "1 1 1 0.7",
			xMin: 0, xMax: 1, yMin: 0f, yMax: 0.015f);
	}
	public void TabPanelText(CUI cui, CuiElementContainer container, string parent, string text, int size, string color, float height, float offset, TextAnchor align, CUI.Handler.FontTypes font)
	{
		cui.CreateText(container, parent: parent, id: $"{parent}text",
			color: color,
			text: text, size,
			xMin: 0.025f, xMax: 0.98f, yMin: offset, yMax: offset + height,
			align: align,
			font: font);
	}
	public void TabPanelButton(CUI cui, CuiElementContainer container, string parent, string text, string command, float height, float offset, Tab.OptionButton.Types type = Tab.OptionButton.Types.None, TextAnchor align = TextAnchor.MiddleCenter)
	{
		var color = type switch
		{
			Tab.OptionButton.Types.Selected => "0.4 0.7 0.2 0.7",
			Tab.OptionButton.Types.Warned => "0.8 0.7 0.2 0.7",
			Tab.OptionButton.Types.Important => "0.97 0.2 0.1 0.7",
			_ => "0.2 0.2 0.2 0.5",
		};

		cui.CreateProtectedButton(container, parent: parent, id: $"{parent}btn",
			color: color,
			textColor: "1 1 1 0.5",
			text: text, 11,
			xMin: 0.015f, xMax: 0.985f, yMin: offset, yMax: offset + height,
			command: command,
			align: align,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);
	}
	public void TabPanelToggle(CUI cui, CuiElementContainer container, string parent, string text, string command, float height, float offset, bool isOn)
	{
		var toggleButtonScale = 0.94f;

		cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: "0.2 0.2 0.2 0.5",
			xMin: 0, xMax: toggleButtonScale, yMin: 0, yMax: 0.015f);

		var button = cui.CreateProtectedButton(container, parent: parent, id: $"{parent}btn",
			color: "0.2 0.2 0.2 0.5",
			textColor: "1 1 1 0.5",
			text: string.Empty, 11,
			xMin: toggleButtonScale, xMax: 0.985f, yMin: offset, yMax: offset + height,
			command: command,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		if (isOn)
		{
			cui.CreateImage(container, button, null,
				url: "checkmark",
				color: "0.4 0.7 0.2 0.7",
				xMin: 0.15f, xMax: 0.85f, yMin: 0.15f, yMax: 0.85f);
		}
	}
	public void TabPanelInput(CUI cui, CuiElementContainer container, string parent, string text, string placeholder, string command, int characterLimit, bool readOnly, float height, float offset, Tab.OptionButton.Types type = Tab.OptionButton.Types.None)
	{
		var color = type switch
		{
			Tab.OptionButton.Types.Selected => "0.4 0.7 0.2 0.7",
			Tab.OptionButton.Types.Warned => "0.8 0.7 0.2 0.7",
			Tab.OptionButton.Types.Important => "0.97 0.2 0.1 0.7",
			_ => "0.2 0.2 0.2 0.5",
		};

		cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		var inPanel = cui.CreatePanel(container, $"{parent}panel", $"{parent}inppanel",
			color: color,
			xMin: OptionWidth, xMax: 0.985f, yMin: 0, yMax: 1);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: color,
			xMin: 0, xMax: OptionWidth, yMin: 0, yMax: 0.015f);

		cui.CreateProtectedInputField(container, parent: inPanel, id: null,
			color: $"1 1 1 {(readOnly ? 0.2f : 1f)}",
			text: placeholder, 11,
			xMin: 0.03f, xMax: 1, yMin: 0, yMax: 1,
			command: command,
			align: TextAnchor.MiddleLeft,
			characterLimit: characterLimit,
			readOnly: readOnly,
			needsKeyboard: true,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		if (!readOnly)
		{
			cui.CreatePanel(container, inPanel, null,
				color: CUI.Color("#4287f5", 0.8f),
				xMin: 0, xMax: 1, yMin: 0, yMax: 0.05f,
				OxMax: -0.5f);
		}
	}
	public void TabPanelEnum(CUI cui, CuiElementContainer container, string parent, string text, string value, string command, float height, float offset, Tab.OptionButton.Types type = Tab.OptionButton.Types.Selected)
	{
		var color = type switch
		{
			Tab.OptionButton.Types.Selected => "0.4 0.7 0.2 0.7",
			Tab.OptionButton.Types.Warned => "0.8 0.7 0.2 0.7",
			Tab.OptionButton.Types.Important => "0.97 0.2 0.1 0.7",
			_ => "0.2 0.2 0.2 0.5",
		};

		cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreatePanel(container, $"{parent}panel", $"{parent}inppanel",
			color: "0.2 0.2 0.2 0.5",
			xMin: OptionWidth, xMax: 0.985f, yMin: 0, yMax: 1);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: "0.2 0.2 0.2 0.5",
			xMin: 0, xMax: OptionWidth, yMin: 0, yMax: 0.015f);

		cui.CreateText(container, parent: $"{parent}inppanel", id: null,
			color: "1 1 1 0.7",
			text: value, 11,
			xMin: 0, xMax: 1, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleCenter,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateProtectedButton(container, parent: $"{parent}inppanel", id: null,
			color: color,
			textColor: "1 1 1 0.7",
			text: "<", 10,
			xMin: 0f, xMax: 0.15f, yMin: 0, yMax: 1,
			command: $"{command} true",
			align: TextAnchor.MiddleCenter,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateProtectedButton(container, parent: $"{parent}inppanel", id: null,
			color: color,
			textColor: "1 1 1 0.7",
			text: ">", 10,
			xMin: 0.85f, xMax: 1f, yMin: 0, yMax: 1,
			command: $"{command} false",
			align: TextAnchor.MiddleCenter,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);
	}
	public void TabPanelRadio(CUI cui, CuiElementContainer container, string parent, string text, bool isOn, string command, float height, float offset)
	{
		var toggleButtonScale = 0.93f;

		cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: "0.2 0.2 0.2 0.5",
			xMin: 0, xMax: toggleButtonScale, yMin: 0, yMax: 0.015f);

		cui.CreateProtectedButton(container, parent: parent, id: $"{parent}btn",
			color: "0.2 0.2 0.2 0.5",
			textColor: "1 1 1 0.5",
			text: string.Empty, 11,
			xMin: toggleButtonScale, xMax: 0.985f, yMin: offset, yMax: offset + height,
			command: command,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		if (isOn)
		{
			cui.CreatePanel(container, $"{parent}btn", null,
				color: "0.4 0.7 0.2 0.7",
				xMin: 0.2f, xMax: 0.8f, yMin: 0.2f, yMax: 0.8f);
		}
	}
	public void TabPanelDropdown(CUI cui, AdminPlayer.Page page, CuiElementContainer container, string parent, string text, string command, float height, float offset, int index, string[] options, string[] optionsIcons, float optionsIconsScale, bool display, Tab.OptionButton.Types type = Tab.OptionButton.Types.Selected)
	{
		var color = type switch
		{
			Tab.OptionButton.Types.Selected => "0.4 0.7 0.2",
			Tab.OptionButton.Types.Warned => "0.8 0.7 0.2",
			Tab.OptionButton.Types.Important => "0.97 0.2 0.1",
			_ => "0.2 0.2 0.2",
		};

		cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreatePanel(container, $"{parent}panel", $"{parent}inppanel",
			color: "0.2 0.2 0.2 0.5",
			xMin: OptionWidth, xMax: 0.985f, yMin: 0, yMax: 1);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: "0.2 0.2 0.2 0.5",
			xMin: 0, xMax: OptionWidth, yMin: 0, yMax: 0.015f);

		var icon = optionsIcons != null && index <= optionsIcons.Length - 1 ? optionsIcons[index] : null;
		var iconXmin = 0.015f;
		var iconXmax = 0.072f;
		var iconYmin = 0.2f;
		var iconYmax = 0.8f;

		var button = cui.CreateProtectedButton(container, parent: $"{parent}inppanel", id: null,
			color: $"0.2 0.2 0.2 0.7",
			textColor: "0 0 0 0",
			text: string.Empty, 0,
			xMin: 0f, xMax: 1f, yMin: 0, yMax: 1,
			command: $"{command} false",
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateText(container, parent: button, null, "1 1 1 0.7", options[index], 10,
			xMin: string.IsNullOrEmpty(icon) ? 0.02f : 0.085f, xMax: 1f, yMin: 0f, yMax: 1f, align: TextAnchor.MiddleLeft);

		if (!string.IsNullOrEmpty(icon))
		{
			cui.CreateImage(container, button, null, icon, optionsIconsScale, "1 1 1 0.7",
				xMin: iconXmin, xMax: iconXmax, yMin: iconYmin, yMax: iconYmax);
		}

		if (display)
		{
			var _spacing = 22;
			var _offset = -_spacing;
			var contentsPerPage = 10;
			var rowPage = options.Skip(contentsPerPage * page.CurrentPage).Take(contentsPerPage);
			var rowPageCount = rowPage.Count();
			var shiftOffset = 15;
			page.TotalPages = (int)Math.Ceiling((double)options.Length / contentsPerPage - 1);
			page.Check();

			for (int i = 0; i < rowPageCount; i++)
			{
				var actualI = i + (page.CurrentPage * contentsPerPage);
				var current = options[actualI];
				var isSelected = actualI == index;

				var subIcon = optionsIcons != null && actualI <= optionsIcons.Length - 1 ? optionsIcons[actualI] : null;

				var subButton = cui.CreateProtectedButton(container, parent: $"{parent}inppanel", id: null,
					color: isSelected ? $"{color} 1" : "0.1 0.1 0.1 1",
					textColor: "0 0 0 0",
					text: string.Empty, 0,
					xMin: 0f, xMax: 1f, yMin: 0, yMax: 1,
					OyMin: _offset, OyMax: _offset,
					OxMin: shiftOffset,
					command: $"{command} true call {actualI}",
					align: TextAnchor.MiddleLeft,
					font: CUI.Handler.FontTypes.RobotoCondensedRegular);

				cui.CreateText(container, parent: subButton, null, isSelected ? "1 1 1 0.7" : "1 1 1 0.4", current, 10,
					xMin: string.IsNullOrEmpty(subIcon) ? 0.035f : 0.085f, xMax: 1f, yMin: 0f, yMax: 1f, align: TextAnchor.MiddleLeft);

				if (!string.IsNullOrEmpty(subIcon))
				{
					cui.CreateImage(container, subButton, null, subIcon, optionsIconsScale, isSelected ? "1 1 1 0.7" : "1 1 1 0.4",
						xMin: iconXmin, xMax: iconXmax, yMin: iconYmin, yMax: iconYmax);
				}

				_offset -= _spacing;
			}

			if (page.TotalPages > 0)
			{
				var controls = cui.CreatePanel(container, parent: $"{parent}inppanel", id: null, "0.2 0.2 0.2 0.2",
					OyMin: _offset, OyMax: _offset - 2,
					OxMin: shiftOffset);

				var id = cui.CreatePanel(container, controls, id: $"{parent}dropdown",
					color: "0.3 0.3 0.3 0.3",
					xMin: 0f, xMax: 1f, yMin: 0, yMax: 1);

				cui.CreateText(container, parent: id, id: null,
					color: "1 1 1 0.5",
					text: $"{page.CurrentPage + 1:n0} / {page.TotalPages + 1:n0}", 9,
					xMin: 0.5f, xMax: 1f, yMin: 0, yMax: 1,
					align: TextAnchor.MiddleLeft,
					font: CUI.Handler.FontTypes.RobotoCondensedRegular);

				#region Left

				cui.CreateProtectedButton(container, parent: id, id: null,
					color: page.CurrentPage > 0 ? "0.8 0.7 0.2 0.7" : "0.3 0.3 0.3 0.1",
					textColor: "1 1 1 0.5",
					text: "<<", 8,
					xMin: 0, xMax: 0.1f, yMin: 0f, yMax: 1f,
					command: $"{command} true --",
					font: CUI.Handler.FontTypes.RobotoCondensedRegular);

				cui.CreateProtectedButton(container, parent: id, id: null,
					color: "0.4 0.7 0.2 0.7",
					textColor: "1 1 1 0.5",
					text: "<", 8,
					xMin: 0.1f, xMax: 0.2f, yMin: 0f, yMax: 1f,
					command: $"{command} true -1",
					font: CUI.Handler.FontTypes.RobotoCondensedRegular);

				#endregion

				#region Right

				cui.CreateProtectedButton(container, parent: id, id: null,
					color: page.CurrentPage < page.TotalPages ? "0.8 0.7 0.2 0.7" : "0.3 0.3 0.3 0.1",
					textColor: "1 1 1 0.5",
					text: ">>", 8,
					xMin: 0.9f, xMax: 1f, yMin: 0f, yMax: 1f,
					command: $"{command} true ++",
					font: CUI.Handler.FontTypes.RobotoCondensedRegular);

				cui.CreateProtectedButton(container, parent: id, id: null,
					color: "0.4 0.7 0.2 0.7",
					textColor: "1 1 1 0.5",
					text: ">", 8,
					xMin: 0.8f, xMax: 0.9f, yMin: 0f, yMax: 1f,
					command: $"{command} true 1",
					font: CUI.Handler.FontTypes.RobotoCondensedRegular);

				#endregion
			}
		}
	}
	public void TabPanelRange(CUI cui, CuiElementContainer container, string parent, string text, string command, string valueText, float min, float max, float value, float height, float offset, Tab.OptionButton.Types type = Tab.OptionButton.Types.None)
	{
		var color = type switch
		{
			Tab.OptionButton.Types.Selected => "0.4 0.7 0.2 0.7",
			Tab.OptionButton.Types.Warned => "0.8 0.7 0.2 0.7",
			Tab.OptionButton.Types.Important => "0.97 0.2 0.1 0.7",
			_ => "0.2 0.2 0.2 0.5",
		};

		cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: color,
			xMin: 0, xMax: OptionWidth, yMin: 0, yMax: 0.015f);

		var panel = cui.CreatePanel(container, $"{parent}panel", $"{parent}inppanel",
			color: color,
			xMin: OptionWidth, xMax: 0.985f, yMin: 0, yMax: 1);

		cui.CreatePanel(container, panel, null,
			color: CUI.Color("#f54242", 0.8f),
			xMin: 0, xMax: value.Scale(min, max, 0f, 1f), yMin: 0, yMax: 1);

		cui.CreateText(container, panel, null, "1 1 1 1", valueText, 8);

		var cuts = max.Clamp(min, RangeCuts);
		var offsetScale = 1f / cuts;
		var currentOffset = 0f;

		for (int i = 0; i < cuts; i++)
		{
			cui.CreateProtectedButton(container, panel, null, "0 0 0 0", "0 0 0 0", string.Empty, 0,
				xMin: currentOffset, xMax: currentOffset + offsetScale, yMin: 0, yMax: 1,
				command: $"{command} {i}");

			currentOffset += offsetScale;
		}
	}
	public void TabPanelButtonArray(CUI cui, CuiElementContainer container, string parent, string command, float spacing, float height, float offset, params Tab.OptionButton[] buttons)
	{
		var panel = cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		var cuts = (1f / buttons.Length) - spacing;
		var currentOffset = 0f;

		for (int i = 0; i < buttons.Length; i++)
		{
			var button = buttons[i];
			var color = (button.Type == null ? Tab.OptionButton.Types.None : button.Type(null)) switch
			{
				Tab.OptionButton.Types.Selected => "0.4 0.7 0.2 0.7",
				Tab.OptionButton.Types.Warned => "0.8 0.7 0.2 0.7",
				Tab.OptionButton.Types.Important => "0.97 0.2 0.1 0.7",
				_ => "0.2 0.2 0.2 0.5",
			};
			cui.CreateProtectedButton(container, panel, null, color, "1 1 1 0.5", button.Name, 11,
				xMin: currentOffset, xMax: currentOffset + cuts, yMin: 0, yMax: 1,
				command: $"{command} {i}");

			currentOffset += cuts + spacing;
		}
	}
	public void TabPanelInputButton(CUI cui, CuiElementContainer container, string parent, string text, string command, float buttonPriority, Tab.OptionInput input, Tab.OptionButton button, float height, float offset)
	{
		var color = "0.2 0.2 0.2 0.5";
		var buttonColor = (button.Type == null ? Tab.OptionButton.Types.None : button.Type(null)) switch
		{
			Tab.OptionButton.Types.Selected => "0.4 0.7 0.2 0.7",
			Tab.OptionButton.Types.Warned => "0.8 0.7 0.2 0.7",
			Tab.OptionButton.Types.Important => "0.97 0.2 0.1 0.7",
			_ => "0.2 0.2 0.2 0.5",
		};

		var panel = cui.CreatePanel(container, parent, $"{parent}panel",
			color: "0.2 0.2 0.2 0",
			xMin: 0, xMax: 1f, yMin: offset, yMax: offset + height);

		cui.CreateText(container, parent: $"{parent}panel", id: $"{parent}text",
			color: "1 1 1 0.7",
			text: $"{text}:", 12,
			xMin: 0.025f, xMax: 0.98f, yMin: 0, yMax: 1,
			align: TextAnchor.MiddleLeft,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		var inPanel = cui.CreatePanel(container, $"{parent}panel", $"{parent}inppanel",
			color: color,
			xMin: OptionWidth, xMax: 0.985f, yMin: 0, yMax: 1);

		cui.CreatePanel(container, $"{parent}panel", null,
			color: color,
			xMin: 0, xMax: OptionWidth, yMin: 0, yMax: 0.015f);

		cui.CreateProtectedInputField(container, parent: inPanel, id: null,
			color: $"1 1 1 {(input.ReadOnly ? 0.2f : 1f)}",
			text: input.Placeholder?.Invoke(), 11,
			xMin: 0.03f, xMax: 1f - buttonPriority, yMin: 0, yMax: 1,
			command: $"{command} input",
			align: TextAnchor.MiddleLeft,
			characterLimit: input.CharacterLimit,
			readOnly: input.ReadOnly,
			needsKeyboard: true,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		cui.CreateProtectedButton(container, parent: inPanel, id: null,
			color: buttonColor,
			textColor: "1 1 1 0.5",
			text: button.Name, 11,
			xMin: 1f - buttonPriority, xMax: 1f, yMin: 0f, yMax: 1f,
			command: $"{command} button",
			align: button.Align,
			font: CUI.Handler.FontTypes.RobotoCondensedRegular);

		if (!input.ReadOnly)
		{
			cui.CreatePanel(container, inPanel, null,
				color: CUI.Color("#4287f5", 0.8f),
				xMin: 0, xMax: 1f - buttonPriority, yMin: 0, yMax: 0.05f,
				OxMax: -0.5f);
		}
	}

	#endregion

	#region UI Commands

	[UiCommand(PanelId + ".changetab")]
	private void ChangeTab(Arg args)
	{
		var player = args.Player();
		var ap = GetOrCreateAdminPlayer(player);
		var previous = ap.TabIndex;

		foreach (var tab in Tabs)
		{
			tab?.OnChange?.Invoke(ap, tab);
		}

		ap.Clear();

		if (int.TryParse(args.Args[0], out int index))
		{
			ap.TabIndex = index;
		}
		else
		{
			ap.TabIndex += args.Args[0] == "up" ? 1 : -1;

			if (ap.TabIndex > Tabs.Count - 1) ap.TabIndex = 0;
			else if (ap.TabIndex < 0) ap.TabIndex = Tabs.Count - 1;
		}

		if (ap.TabIndex != previous) Draw(player);
	}

	[UiCommand(PanelId + ".callaction")]
	private void CallAction(Arg args)
	{
		var player = args.Player();

		if (CallColumnRow(player, args.Args[0].ToInt(), args.Args[1].ToInt(), args.Args.Skip(2).ToArray()))
			Draw(player);
	}

	[UiCommand(PanelId + ".changecolumnpage")]
	private void ChangeColumnPage(Arg args)
	{
		var player = args.Player();
		var instance = GetOrCreateAdminPlayer(player);
		var page = instance.GetOrCreatePage(args.Args[0].ToInt());
		var type = args.Args[1].ToInt();

		switch (type)
		{
			case 0:
				page.CurrentPage--;
				if (page.CurrentPage < 0) page.CurrentPage = page.TotalPages;
				break;

			case 1:
				page.CurrentPage++;
				if (page.CurrentPage > page.TotalPages) page.CurrentPage = 0;
				break;

			case 2:
				page.CurrentPage = 0;
				break;

			case 3:
				page.CurrentPage = page.TotalPages;
				break;

			case 4:
				page.CurrentPage = (args.Args[2].ToInt() - 1).Clamp(0, page.TotalPages);
				break;
		}

		Draw(player);
	}

	[UiCommand(PanelId + ".close")]
	private void CloseUI(Arg args)
	{
		Close(args.Player());
	}

	#endregion

	#region Methods

	public void Draw(BasePlayer player)
	{
		try
		{
			var ap = GetOrCreateAdminPlayer(player);
			var tab = GetTab(player);
			ap.IsInMenu = true;

			using var cui = new CUI(Handler);

			var container = cui.CreateContainer(PanelId,
				color: "0 0 0 0.75",
				xMin: 0, xMax: 1, yMin: 0, yMax: 1,
				needsCursor: true, destroyUi: PanelId, parent: CUI.ClientPanels.Hud);

			cui.CreatePanel(container, parent: PanelId, id: "color",
				color: "0 0 0 0.6",
				xMin: 0.15f, xMax: 0.85f, yMin: 0.1f, yMax: 0.9f);
			cui.CreatePanel(container, "color", "main",
				color: "0 0 0 0.5",
				blur: true);

			#region Title

			cui.CreateText(container, parent: "main", id: null,
				color: "1 1 1 0.8",
				text: "<b>Admin Settings</b>", 18,
				xMin: 0.0175f, yMin: 0.8f, xMax: 1f, yMax: 0.97f,
				align: TextAnchor.UpperLeft,
				font: CUI.Handler.FontTypes.RobotoCondensedBold);

			#endregion

			#region Exit

			cui.CreateProtectedButton(container, parent: "main", id: null,
				color: "0.6 0.2 0.2 0.9",
				textColor: "1 0.5 0.5 1",
				text: "X", 10,
				xMin: 0.97f, xMax: 0.99f, yMin: 0.96f, yMax: 0.99f,
				command: PanelId + ".close",
				font: CUI.Handler.FontTypes.DroidSansMono);

			#endregion

			#region Tabs

			cui.CreatePanel(container, parent: "main", id: "tab_buttons",
				color: "0 0 0 0.6",
				xMin: 0.01f, xMax: 0.99f, yMin: 0.875f, yMax: 0.92f);

			TabButton(cui, container, "tab_buttons", "<", PanelId + ".changetab down", 0.03f, 0);
			TabButton(cui, container, "tab_buttons", ">", PanelId + ".changetab up", 0.03f, 0.97f);

			var tabIndex = 0.03f;
			var amount = Tabs.Count;
			var tabWidth = amount == 0 ? 0f : 0.94f / amount;

			for (int i = ap.TabSkip; i < amount; i++)
			{
				var _tab = Tabs[ap.TabSkip + i];
				var plugin = _tab.Plugin.IsCorePlugin ? string.Empty : $"<size=8>\n{_tab.Plugin?.Name} ({_tab.Plugin?.Version}) by {_tab.Plugin?.Author}";
				TabButton(cui, container, "tab_buttons", $"{(ap.TabIndex == i ? $"<b>{_tab.Name}</b>" : _tab.Name)}{plugin}", PanelId + $".changetab {i}", tabWidth, tabIndex, ap.TabIndex == i);
				tabIndex += tabWidth;
			}

			#endregion

			#region Panels

			cui.CreatePanel(container, "main", "panels",
				color: "0 0 0 0",
				xMin: 0.01f, xMax: 0.99f, yMin: 0.02f, yMax: 0.86f);

			if (tab != null)
			{
				#region Override

				tab.Override?.Invoke(tab, container, "panels", ap);

				#endregion

				if (tab.Override == null)
				{
					#region Columns

					var panelIndex = 0f;
					var spacing = 0.005f;
					var panelWidth = (tab.Columns.Count == 0 ? 0f : 1f / tab.Columns.Count) - spacing;

					for (int i = 0; i < tab.Columns.Count; i++)
					{
						var panel = $"sub{i}";
						var rows = tab.Columns[i];

						cui.CreatePanel(container, "panels", panel,
							color: "0 0 0 0.5",
							xMin: panelIndex, xMax: panelIndex + panelWidth - spacing, yMin: 0, yMax: 1);

						#region Rows

						var columnPage = ap.GetOrCreatePage(i);
						var contentsPerPage = 19;
						var rowSpacing = 0.01f;
						var rowHeight = 0.04f;
						var rowPage = rows.Skip(contentsPerPage * columnPage.CurrentPage).Take(contentsPerPage);
						var rowPageCount = rowPage.Count();
						columnPage.TotalPages = (int)Math.Ceiling((double)rows.Count / contentsPerPage - 1);
						columnPage.Check();
						var rowIndex = (rowHeight + rowSpacing) * (contentsPerPage - (rowPageCount - (columnPage.TotalPages > 0 ? 0 : 1)));

						if (columnPage.TotalPages > 0)
						{
							TabColumnPagination(cui, container, panel, i, columnPage, rowHeight, rowIndex);
							TabColumnPagination(cui, container, panel, i, columnPage, rowHeight, rowIndex);

							rowIndex += rowHeight + rowSpacing;
						}

						for (int r = rowPageCount; r-- > 0;)
						{
							var actualI = r + (columnPage.CurrentPage * contentsPerPage);
							var row = rows.ElementAt(actualI);

							switch (row)
							{
								case Tab.OptionName name:
									TabPanelName(cui, container, panel, name.Name, rowHeight, rowIndex, name.Align);
									break;

								case Tab.OptionButton button:
									TabPanelButton(cui, container, panel, button.Name, PanelId + $".callaction {i} {actualI}", rowHeight, rowIndex, button.Type == null ? Tab.OptionButton.Types.None : button.Type.Invoke(ap), button.Align);
									break;

								case Tab.OptionText text:
									TabPanelText(cui, container, panel, text.Name, text.Size, text.Color, rowHeight, rowIndex, text.Align, text.Font);
									break;

								case Tab.OptionInput input:
									TabPanelInput(cui, container, panel, input.Name, input.Placeholder?.Invoke(), PanelId + $".callaction {i} {actualI}", input.CharacterLimit, input.ReadOnly, rowHeight, rowIndex);
									break;

								case Tab.OptionEnum @enum:
									TabPanelEnum(cui, container, panel, @enum.Name, @enum.Text?.Invoke(), PanelId + $".callaction {i} {actualI}", rowHeight, rowIndex);
									break;

								case Tab.OptionToggle toggle:
									TabPanelToggle(cui, container, panel, toggle.Name, PanelId + $".callaction {i} {actualI}", rowHeight, rowIndex, toggle.IsOn != null ? toggle.IsOn.Invoke(ap) : false);
									break;

								case Tab.OptionRadio radio:
									TabPanelRadio(cui, container, panel, radio.Name, radio.Index == tab.Radios[radio.Id].Selected, PanelId + $".callaction {i} {actualI}", rowHeight, rowIndex);
									break;

								case Tab.OptionDropdown dropdown:
									TabPanelDropdown(cui, ap._selectedDropdownPage, container, panel, dropdown.Name, PanelId + $".callaction {i} {actualI}", rowHeight, rowIndex, dropdown.Index.Invoke(), dropdown.Options, dropdown.OptionsIcons, dropdown.OptionsIconScale, ap._selectedDropdown == dropdown);
									break;

								case Tab.OptionRange range:
									TabPanelRange(cui, container, panel, range.Name, PanelId + $".callaction {i} {actualI}", range.Text?.Invoke(), range.Min, range.Max, range.Value == null ? 0 : range.Value.Invoke(), rowHeight, rowIndex);
									break;

								case Tab.ButtonArray array:
									TabPanelButtonArray(cui, container, panel, PanelId + $".callaction {i} {actualI}", array.Spacing, rowHeight, rowIndex, array.Buttons);
									break;

								case Tab.OptionInputButton inputButton:
									TabPanelInputButton(cui, container, panel, inputButton.Name, PanelId + $".callaction {i} {actualI}", inputButton.ButtonPriority, inputButton.Input, inputButton.Button, rowHeight, rowIndex);
									break;

								default:
									break;
							}

							rowIndex += rowHeight + rowSpacing;
						}

						#endregion

						panelIndex += panelWidth + spacing;
					}

					#endregion
				}
			}

			#endregion

			cui.Send(container, player);
		}
		catch (Exception ex)
		{
			PutsError($"Draw(player) failed.", ex);
		}
	}
	public void DrawCursorLocker(BasePlayer player)
	{
		using var cui = new CUI(Handler);

		var container = cui.CreateContainer(CursorPanelId,
			color: "0 0 0 0",
			xMin: 0, xMax: 0, yMin: 0, yMax: 0,
			fadeIn: 0.005f,
			needsCursor: true);

		cui.Send(container, player);
	}
	public void Close(BasePlayer player)
	{
		Handler.Destroy(PanelId, player);
		Handler.Destroy(CursorPanelId, player);

		var ap = GetOrCreateAdminPlayer(player);
		ap.IsInMenu = false;
	}

	public void RegisterTab(Tab tab, int? insert = null)
	{
		var existentTab = Tabs.FirstOrDefault(x => x.Id == tab.Id);
		if (existentTab != null)
		{
			var index = Tabs.IndexOf(existentTab);
			Tabs.RemoveAt(index);
			Pool.Free(ref existentTab);

			Tabs.Insert(insert ?? index, tab);
		}
		else
		{
			if (insert != null) Tabs.Insert(insert.Value, tab);
			else Tabs.Add(tab);
		}

		AdminPlayers.Clear();
	}
	public void UnregisterTab(string id)
	{
		AdminPlayers.Clear();

		var tab = Tabs.FirstOrDefault(x => x.Id == id);

		if (tab != null)
		{
			tab.Dispose();
		}

		Tabs.RemoveAll(x => x.Id == id);
	}

	public AdminPlayer GetOrCreateAdminPlayer(BasePlayer player)
	{
		if (AdminPlayers.TryGetValue(player, out AdminPlayer adminPlayer)) return adminPlayer;

		adminPlayer = new AdminPlayer(player);
		AdminPlayers.Add(player, adminPlayer);
		return adminPlayer;
	}
	public void SetTab(BasePlayer player, string id)
	{
		var ap = GetOrCreateAdminPlayer(player);
		var previous = ap.TabIndex;

		var tab = Tabs.FirstOrDefault(x => x.Id == id);
		if (tab != null)
		{
			ap.TabIndex = Tabs.IndexOf(tab);
			tab?.OnChange?.Invoke(ap, tab);
		}

		if (ap.TabIndex != previous) Draw(player);
	}
	public Tab GetTab(BasePlayer player)
	{
		if (Tabs.Count == 0) return null;

		var adminPlayer = GetOrCreateAdminPlayer(player);
		if (adminPlayer.TabIndex > Tabs.Count - 1 || adminPlayer.TabIndex < 0) return null;

		return Tabs[adminPlayer.TabIndex];
	}
	public Tab FindTab(string id)
	{
		return Tabs.FirstOrDefault(x => x.Id == id);
	}
	public bool CallColumnRow(BasePlayer player, int column, int row, string[] args)
	{
		var ap = GetOrCreateAdminPlayer(player);
		var tab = GetTab(player);

		ap.LastPressedColumn = column;
		ap.LastPressedRow = row;

		switch (tab.Columns[column][row])
		{
			case Tab.OptionButton button:
				button.Callback?.Invoke(ap);
				return button.Callback != null;

			case Tab.OptionInput input:
				input.Callback?.Invoke(ap, args);
				return input.Callback != null;

			case Tab.OptionEnum @enum:
				@enum.Callback?.Invoke(args[0].ToBool());
				return @enum.Callback != null;

			case Tab.OptionToggle toggle:
				toggle.Callback?.Invoke(ap);
				return toggle.Callback != null;

			case Tab.OptionRadio radio:
				if (radio.Radio.Selected != radio.Index)
				{
					radio.Radio.Change(radio.Index, ap);
					radio.Callback?.Invoke(true, ap);
					return true;
				}
				break;

			case Tab.OptionDropdown dropdown:
				var page = ap._selectedDropdownPage;
				switch (args[0].ToBool())
				{
					case true:
						switch (args[1])
						{
							case "call":
								ap._selectedDropdown = null;
								dropdown.Callback?.Invoke(args[2].ToInt());
								page.CurrentPage = 0;
								break;

							default:
								switch (args[1])
								{
									case "--":
										page.CurrentPage = 0;
										break;

									case "++":
										page.CurrentPage = page.TotalPages;
										break;

									default:
										page.CurrentPage = args[1].ToInt();
										break;
								}

								if (page.CurrentPage < 0) page.CurrentPage = page.TotalPages;
								else if (page.CurrentPage > page.TotalPages) page.CurrentPage = 0;
								break;
						}

						return true;

					case false:
						page.CurrentPage = 0;

						var oldSelectedDropdown = ap._selectedDropdown;
						if (oldSelectedDropdown == dropdown)
						{
							ap._selectedDropdown = null;
							return true;
						}
						else
						{
							ap._selectedDropdown = dropdown;
							return oldSelectedDropdown != dropdown;
						}
				}

			case Tab.OptionRange range:
				range.Callback?.Invoke(ap, args[0].ToFloat().Scale(0f, range.Max.Clamp(range.Min, RangeCuts) - 1f, range.Min, range.Max));
				return range.Callback != null;

			case Tab.ButtonArray array:
				var callback = array.Buttons[args[0].ToInt()].Callback;
				callback?.Invoke(ap);
				return callback != null;

			case Tab.OptionInputButton inputButton:
				switch (args[0])
				{
					case "input":
						inputButton.Input.Callback?.Invoke(ap, args.Skip(1).ToArray());
						return inputButton.Input.Callback != null;

					case "button":
						inputButton.Button.Callback?.Invoke(ap);
						return inputButton.Button.Callback != null;
				}
				break;
		}

		return false;
	}

	#endregion

	public class AdminPlayer : IDisposable
	{
		public static AdminPlayer Blank { get; } = new AdminPlayer(null);

		public BasePlayer Player;
		public bool IsInMenu;
		public Dictionary<int, Page> ColumnPages = new();
		public Dictionary<string, object> LocalStorage = new();

		public int TabIndex;
		public int TabSkip;
		public int LastPressedColumn;
		public int LastPressedRow;

		internal Tab.OptionDropdown _selectedDropdown;
		internal Page _selectedDropdownPage = new();

		public AdminPlayer(BasePlayer player)
		{
			Player = player;
		}

		public T GetStorage<T>(string id)
		{
			if (LocalStorage.TryGetValue(id, out var storage)) return (T)storage;

			return default;
		}
		public object GetStorage(string id)
		{
			if (LocalStorage.TryGetValue(id, out var storage)) return storage;

			return default;
		}
		public void SetStorage<T>(string id, T value)
		{
			LocalStorage[id] = value;
		}
		public void ClearStorage(string id)
		{
			LocalStorage[id] = null;
		}
		public void Clear()
		{
			foreach (var page in ColumnPages)
			{
				var value = ColumnPages[page.Key];
				Pool.Free(ref value);
			}

			ColumnPages.Clear();
			LocalStorage.Clear();

			_selectedDropdown = null;
			_selectedDropdownPage.CurrentPage = 0;
		}
		public Page GetOrCreatePage(int column)
		{
			if (ColumnPages.TryGetValue(column, out var page)) return page;
			else
			{
				ColumnPages[column] = page = new Page();
				return page;
			}
		}

		public void Dispose()
		{
			Clear();
		}

		public class Page
		{
			public int CurrentPage { get; set; }
			public int TotalPages { get; set; }

			public void Check()
			{
				if (CurrentPage < 0) CurrentPage = TotalPages;
				else if (CurrentPage > TotalPages) CurrentPage = 0;
			}
		}
	}
	public class Tab : IDisposable
	{
		public string Id;
		public string Name;
		public RustPlugin Plugin;
		public Action<Tab, CuiElementContainer, string, AdminPlayer> Override;
		public Dictionary<int, List<Option>> Columns = new();
		public Action<AdminPlayer, Tab> OnChange;
		public Dictionary<string, Radio> Radios = new();

		public Tab(string id, string name, RustPlugin plugin, Action<AdminPlayer, Tab> onChange = null)
		{
			Id = id;
			Name = name;
			Plugin = plugin;
			OnChange = onChange;
		}

		public void ClearColumn(int column, bool erase = false)
		{
			if (Columns.TryGetValue(column, out var rows))
			{
				rows.Clear();

				if (erase)
				{
					Columns[column] = null;
					Columns.Remove(column);
				}
			}
		}
		public void ClearAfter(int index)
		{
			for (int i = 0; i < Columns.Count; i++)
			{
				if (i >= index) ClearColumn(i);
			}
		}
		public Tab AddColumn(int column)
		{
			if (!Columns.TryGetValue(column, out var options))
			{
				Columns[column] = new List<Option>();
			}

			return this;
		}
		public Tab AddRow(int column, Option row)
		{
			if (Columns.TryGetValue(column, out var options))
			{
				options.Add(row);
			}
			else
			{

				Columns[column] = options = new List<Option>();
				options.Add(row);
			}

			return this;
		}
		public Tab AddName(int column, string name, TextAnchor align = TextAnchor.MiddleLeft)
		{
			return AddRow(column, new OptionName(name, align));
		}
		public Tab AddButton(int column, string name, Action<AdminPlayer> callback, Func<AdminPlayer, OptionButton.Types> type = null, TextAnchor align = TextAnchor.MiddleCenter)
		{
			return AddRow(column, new OptionButton(name, align, callback, type));
		}
		public Tab AddToggle(int column, string name, Action<AdminPlayer> callback, Func<AdminPlayer, bool> isOn = null)
		{
			return AddRow(column, new OptionToggle(name, callback, isOn));
		}
		public Tab AddText(int column, string name, int size, string color, TextAnchor align = TextAnchor.MiddleCenter, CUI.Handler.FontTypes font = CUI.Handler.FontTypes.RobotoCondensedRegular)
		{
			return AddRow(column, new OptionText(name, size, color, align, font));
		}
		public Tab AddInput(int column, string name, Func<string> placeholder, int characterLimit, bool readOnly, Action<AdminPlayer, string[]> callback = null)
		{
			return AddRow(column, new OptionInput(name, placeholder, characterLimit, readOnly, callback));
		}
		public Tab AddInput(int column, string name, Func<string> placeholder, Action<AdminPlayer, string[]> callback = null)
		{
			return AddInput(column, name, placeholder, 0, callback == null, callback);
		}
		public Tab AddEnum(int column, string name, Action<bool> callback, Func<string> text)
		{
			AddRow(column, new OptionEnum(name, callback, text));
			return this;
		}
		public Tab AddRadio(int column, string name, string id, bool wantsOn, Action<bool, AdminPlayer> callback = null)
		{
			if (!Radios.TryGetValue(id, out var radio))
			{
				Radios[id] = radio = new();
			}

			radio.TemporaryIndex++;
			if (wantsOn) radio.Selected = radio.TemporaryIndex;

			var index = radio.TemporaryIndex;
			var option = new OptionRadio(name, id, index, wantsOn, callback, radio);
			radio.Options.Add(option);

			return AddRow(column, option);
		}
		public Tab AddDropdown(int column, string name, Func<int> index, Action<int> callback, string[] options, string[] optionsIcons = null, float optionsIconScale = 0f)
		{
			AddRow(column, new OptionDropdown(name, index, callback, options, optionsIcons, optionsIconScale));
			return this;
		}
		public Tab AddRange(int column, string name, float min, float max, Func<float> value, Action<AdminPlayer, float> callback, Func<string> text = null)
		{
			AddRow(column, new OptionRange(name, min, max, value, callback, text));
			return this;
		}
		public Tab AddButtonArray(int column, float spacing, params OptionButton[] buttons)
		{
			AddRow(column, new ButtonArray(string.Empty, spacing, buttons));
			return this;
		}
		public Tab AddButtonArray(int column, params OptionButton[] buttons)
		{
			AddRow(column, new ButtonArray(string.Empty, 0.01f, buttons));
			return this;
		}
		public Tab AddInputButton(int column, string name, float buttonPriority, OptionInput input, OptionButton button)
		{
			AddRow(column, new OptionInputButton(name, buttonPriority, input, button));
			return this;
		}

		public void Dispose()
		{
			foreach (var column in Columns)
			{
				column.Value.Clear();
			}

			Columns.Clear();
			Columns = null;
		}

		public class Radio : IDisposable
		{
			public int Selected;

			public int TemporaryIndex = -1;

			public List<OptionRadio> Options = new();

			public void Change(int index, AdminPlayer ap)
			{
				Options[Selected]?.Callback?.Invoke(false, ap);

				Selected = index;
			}

			public void Dispose()
			{
				Options.Clear();
				Options = null;
			}
		}

		public class Option
		{
			public string Name;

			public Option(string name)
			{
				Name = name;
			}
		}
		public class OptionName : Option
		{
			public TextAnchor Align;

			public OptionName(string name, TextAnchor align) : base(name) { Align = align; }
		}
		public class OptionText : Option
		{
			public int Size;
			public string Color;
			public TextAnchor Align;
			public CUI.Handler.FontTypes Font;

			public OptionText(string name, int size, string color, TextAnchor align, CUI.Handler.FontTypes font) : base(name) { Align = align; Size = size; Color = color; Font = font; }
		}
		public class OptionInput : Option
		{
			public Func<string> Placeholder;
			public int CharacterLimit;
			public bool ReadOnly;
			public Action<AdminPlayer, string[]> Callback;

			public OptionInput(string name, Func<string> placeholder, int characterLimit, bool readOnly, Action<AdminPlayer, string[]> args) : base(name)
			{
				Placeholder = placeholder;
				Callback = args;
				CharacterLimit = characterLimit;
				ReadOnly = readOnly;
			}
		}
		public class OptionButton : Option
		{
			public Func<AdminPlayer, Types> Type;
			public Action<AdminPlayer> Callback;
			public TextAnchor Align = TextAnchor.MiddleCenter;

			public enum Types
			{
				None,
				Selected,
				Warned,
				Important
			}

			public OptionButton(string name, TextAnchor align, Action<AdminPlayer> callback, Func<AdminPlayer, Types> type = null) : base(name) { Align = align; Callback = callback; Type = type; }
			public OptionButton(string name, Action<AdminPlayer> callback, Func<AdminPlayer, Types> type = null) : base(name) { Callback = callback; Type = type; }
		}
		public class OptionToggle : Option
		{
			public Func<AdminPlayer, bool> IsOn;
			public Action<AdminPlayer> Callback;

			public OptionToggle(string name, Action<AdminPlayer> callback, Func<AdminPlayer, bool> isOn = null) : base(name) { Callback = callback; IsOn = isOn; }
		}
		public class OptionEnum : Option
		{
			public Func<string> Text;
			public Action<bool> Callback;

			public OptionEnum(string name, Action<bool> callback, Func<string> text) : base(name) { Callback = callback; Text = text; }
		}
		public class OptionRange : Option
		{
			public float Min = 0;
			public float Max = 1;
			public Func<float> Value;
			public Action<AdminPlayer, float> Callback;
			public Func<string> Text;

			public OptionRange(string name, float min, float max, Func<float> value, Action<AdminPlayer, float> callback, Func<string> text) : base(name) { Min = min; Max = max; Callback = callback; Value = value; Text = text; }
		}
		public class OptionRadio : Option
		{
			public string Id;
			public int Index;
			public bool WantsOn;
			public Action<bool, AdminPlayer> Callback;

			public Radio Radio;

			public OptionRadio(string name, string id, int index, bool on, Action<bool, AdminPlayer> callback, Radio radio) : base(name) { Id = id; Callback = callback; WantsOn = on; Index = index; Radio = radio; }
		}
		public class OptionDropdown : Option
		{
			public Func<int> Index;
			public Action<int> Callback;
			public string[] Options;
			public string[] OptionsIcons;
			public float OptionsIconScale;

			public OptionDropdown(string name, Func<int> index, Action<int> callback, string[] options, string[] optionsIcons, float optionsIconScale) : base(name) { Index = index; Callback = callback; Options = options; OptionsIcons = optionsIcons; OptionsIconScale = optionsIconScale; }
		}
		public class OptionInputButton : Option
		{
			public OptionInput Input;
			public OptionButton Button;
			public float ButtonPriority = 0.25f;

			public OptionInputButton(string name, float buttonPriority, OptionInput input, OptionButton button) : base(name)
			{
				ButtonPriority = buttonPriority;
				Input = input;
				Button = button;
			}
		}
		public class ButtonArray : Option
		{
			public OptionButton[] Buttons;
			public float Spacing = 0.01f;

			public ButtonArray(string name, float spacing, params OptionButton[] buttons) : base(name)
			{
				Buttons = buttons;
				Spacing = spacing;
			}
		}
	}

	#region Core Tabs

	public class CarbonTab
	{
		public static Core.Config Config => Community.Runtime.Config;

		public static Tab Get()
		{
			var tab = new Tab("carbon", "Carbon", Community.Runtime.CorePlugin);
			tab.AddColumn(1);

			tab.AddInput(0, "Host Name", () => $"{ConVar.Server.hostname}", (ap, args) => { ConVar.Server.hostname = args.ToString(" "); });
			tab.AddInput(0, "Level", () => $"{ConVar.Server.level}", null);

			tab.AddName(0, "Info", TextAnchor.MiddleLeft);
			{
				tab.AddInput(0, "Version", () => $"{Community.Version}", null);
				tab.AddInput(0, "Informational Version", () => $"{Community.InformationalVersion}", null);

				var loadedHooks = Community.Runtime.HookManager.LoadedDynamicHooks.Count(x => x.IsInstalled) + Community.Runtime.HookManager.LoadedStaticHooks.Count(x => x.IsInstalled);
				var totalHooks = Community.Runtime.HookManager.LoadedDynamicHooks.Count() + Community.Runtime.HookManager.LoadedStaticHooks.Count();
				tab.AddInput(0, "Hooks", () => $"<b>{loadedHooks:n0}</b> / {totalHooks:n0} loaded", null);
				tab.AddInput(0, "Static Hooks", () => $"{Community.Runtime.HookManager.LoadedStaticHooks.Count():n0}", null);
				tab.AddInput(0, "Dynamic Hooks", () => $"{Community.Runtime.HookManager.LoadedDynamicHooks.Count():n0}", null);

				tab.AddName(0, "Plugins", TextAnchor.MiddleLeft);
				tab.AddInput(0, "Mods", () => $"{Community.Runtime.Plugins.Plugins.Count:n0}", null);

				tab.AddName(0, "Console", TextAnchor.MiddleLeft);

				var command = string.Empty;
				tab.AddInputButton(0, "Execute Server Command", 0.2f, new Tab.OptionInput(null, () => command, 0, false, (ap, args) => { command = args.ToString(" "); }), new Tab.OptionButton("Run", ap =>
				{
					var commandSplit = command.Split(' ');
					Run(Option.Server, commandSplit[0], commandSplit.Skip(1).ToArray());
				}));
			}

			tab.AddName(1, "Config", TextAnchor.MiddleLeft);
			{
				tab.AddToggle(1, "Is Modded", ap => { Config.IsModded = !Config.IsModded; Community.Runtime.SaveConfig(); }, ap => Config.IsModded);
				tab.AddToggle(1, "Auto Update", ap => { Config.AutoUpdate = !Config.AutoUpdate; Community.Runtime.SaveConfig(); }, ap => Config.AutoUpdate);

				tab.AddName(1, "General", TextAnchor.MiddleLeft);
				tab.AddToggle(1, "Carbon Tag", ap => { Config.CarbonTag = !Config.CarbonTag; Community.Runtime.SaveConfig(); }, ap => Config.CarbonTag);
				tab.AddToggle(1, "Hook Time Tracker", ap => { Config.HookTimeTracker = !Config.HookTimeTracker; Community.Runtime.SaveConfig(); }, ap => Config.HookTimeTracker);
				tab.AddToggle(1, "Hook Validation", ap => { Config.HookValidation = !Config.HookValidation; Community.Runtime.SaveConfig(); }, ap => Config.HookValidation);
				tab.AddInput(1, "Entity Map Buffer Size (restart required)", () => Config.EntityMapBufferSize.ToString(), (ap, args) => { Config.EntityMapBufferSize = args[0].ToInt().Clamp(10000, 500000); Community.Runtime.SaveConfig(); });

				tab.AddName(1, "Watchers", TextAnchor.MiddleLeft);
				tab.AddToggle(1, "Script Watchers", ap => { Config.ScriptWatchers = !Config.ScriptWatchers; Community.Runtime.SaveConfig(); }, ap => Config.ScriptWatchers);
				tab.AddToggle(1, "Harmony Reference (<color=red>!</color>)", ap => { Config.HarmonyReference = !Config.HarmonyReference; Community.Runtime.SaveConfig(); }, ap => Config.HarmonyReference);
				tab.AddToggle(1, "File Name Check", ap => { Config.FileNameCheck = !Config.FileNameCheck; Community.Runtime.SaveConfig(); }, ap => Config.FileNameCheck);

				tab.AddName(1, "Logging", TextAnchor.MiddleLeft);
				tab.AddInput(1, "Log File Mode", () => Config.LogFileMode.ToString(), (ap, args) => { Config.LogFileMode = args[0].ToInt().Clamp(0, 2); Community.Runtime.SaveConfig(); });
				tab.AddInput(1, "Log Verbosity (Debug)", () => Config.LogVerbosity.ToString(), (ap, args) => { Config.LogVerbosity = args[0].ToInt().Clamp(0, 10); Community.Runtime.SaveConfig(); });
				tab.AddDropdown(1, "Log Severity", () => (int)Config.LogSeverity, index => { Config.LogSeverity = (Logger.Severity)index; Community.Runtime.SaveConfig(); }, Enum.GetNames(typeof(Logger.Severity)));

				tab.AddName(1, "Miscellaneous", TextAnchor.MiddleLeft);
				tab.AddInput(1, "Server Language", () => Config.Language, (ap, args) => { Config.Language = args[0]; Community.Runtime.SaveConfig(); });
				tab.AddInput(1, "WebRequest IP", () => Config.WebRequestIp, (ap, args) => { Config.WebRequestIp = args[0]; Community.Runtime.SaveConfig(); });
				tab.AddEnum(1, "Permission Mode", back => { var e = Enum.GetNames(typeof(Permission.SerializationMode)); Config.PermissionSerialization += back ? -1 : 1; if (Config.PermissionSerialization < 0) Config.PermissionSerialization = Permission.SerializationMode.SQL; else if (Config.PermissionSerialization > Permission.SerializationMode.SQL) Config.PermissionSerialization = Permission.SerializationMode.Protobuf; Community.Runtime.SaveConfig(); }, () => Config.PermissionSerialization.ToString());
			}

			return tab;
		}
	}
	public class PermissionsTab
	{
		internal static Permission permission;

		public static Tab Get()
		{
			permission = Community.Runtime.CorePlugin.permission;

			var perms = new Tab("permissions", "Permissions", Community.Runtime.CorePlugin, (instance, tab) =>
			{
				tab.ClearColumn(1);
				tab.ClearColumn(2);
				tab.ClearColumn(3);
				GeneratePlayers(tab, permission, instance);
			});
			{
				perms.AddName(0, "Options", TextAnchor.MiddleLeft);

				perms.AddRow(0, new Tab.OptionButton($"Players", instance =>
				{
					perms.ClearColumn(1);
					perms.ClearColumn(2);
					perms.ClearColumn(3);
					instance.Clear();

					instance.SetStorage("option", 0);

					GeneratePlayers(perms, permission, instance);
				}, type: (instance) => instance.GetStorage<int>("option") == 0 ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));

				GeneratePlayers(perms, permission, AdminPlayer.Blank);

				perms.AddRow(0, new Tab.OptionButton($"Groups", instance =>
				{
					perms.ClearColumn(1);
					perms.ClearColumn(2);
					perms.ClearColumn(3);
					instance.Clear();

					instance.ClearStorage("player");
					instance.ClearStorage("plugin");

					instance.SetStorage("option", 1);

					perms.AddName(1, "Groups", TextAnchor.MiddleLeft);
					{
						foreach (var group in permission.GetGroups())
						{
							perms.AddRow(1, new Tab.OptionButton($"{group}", instance2 =>
							{
								instance.SetStorage("group", group);

								instance.ClearStorage("plugin");

								perms.ClearColumn(2);
								perms.ClearColumn(3);

								perms.AddName(2, "Plugins", TextAnchor.MiddleLeft);
								{
									foreach (var plugin in Community.Runtime.Plugins.Plugins.Where(x => x.permission.GetPermissions().Any(y => y.StartsWith(x.Name.ToLower()))))
									{
										perms.AddRow(2, new Tab.OptionButton($"{plugin.Name} ({plugin.Version})", instance3 =>
										{
											instance.SetStorage("plugin", plugin);

											perms.ClearColumn(3);

											perms.AddName(3, "Permissions", TextAnchor.MiddleLeft);
											foreach (var perm in permission.GetPermissions().Where(x => x.StartsWith(plugin.Name.ToLower())))
											{
												perms.AddRow(3, new Tab.OptionButton($"{perm}", instance5 =>
												{
													if (permission.GroupHasPermission(group, perm))
														permission.RevokeGroupPermission(group, perm);
													else permission.GrantGroupPermission(group, perm, plugin);
												}, type: (_instance) => permission.GroupHasPermission(group, perm) ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
											}
										}, type: (_instance) => instance.GetStorage("plugin") == plugin ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
									}
								}
							}, type: (_instance) => instance.GetStorage<string>("group") == group ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
						}
					}
					perms.AddRow(1, new Tab.OptionButton("Add Group", null, (_instance) => Tab.OptionButton.Types.Warned));
				}, type: (instance) => instance.GetStorage<int>("option") == 1 ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
				perms.AddColumn(1);
				perms.AddColumn(2);
				perms.AddColumn(3);
			}

			return perms;
		}

		public static void GeneratePlayers(Tab perms, Permission permission, AdminPlayer instance)
		{
			perms.ClearColumn(1);
			perms.AddName(1, "Players", TextAnchor.MiddleLeft);
			{
				foreach (var player in BasePlayer.allPlayerList.Where(x => x.userID.IsSteamId()))
				{
					perms.AddRow(1, new Tab.OptionButton($"{player.displayName} ({player.userID})", instance2 =>
					{
						instance.SetStorage("player", player);

						instance.ClearStorage("plugin");

						perms.ClearColumn(3);

						GeneratePlugins(perms, instance, permission, player);
					}, type: (_instance) => instance.GetStorage<BasePlayer>("player") == player ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
				}
			}
		}

		public static void GeneratePlugins(Tab perms, AdminPlayer instance, Permission permission, BasePlayer player)
		{
			perms.ClearColumn(2);
			perms.AddName(2, "Plugins", TextAnchor.MiddleLeft);
			{
				foreach (var plugin in Community.Runtime.Plugins.Plugins.Where(x => x.permission.GetPermissions().Any(y => y.StartsWith(x.Name.ToLower()))))
				{
					perms.AddRow(2, new Tab.OptionButton($"{plugin.Name} ({plugin.Version})", instance3 =>
					{
						instance.SetStorage("plugin", plugin);
						instance.SetStorage("pluginr", instance3.LastPressedRow);
						instance.SetStorage("pluginc", instance3.LastPressedColumn);

						GeneratePermissions(perms, permission, plugin, player);
					}, type: (_instance) => instance.GetStorage("plugin") == plugin ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
				}
			}
		}

		public static void GeneratePermissions(Tab perms, Permission permission, RustPlugin plugin, BasePlayer player)
		{
			perms.ClearColumn(3);
			perms.AddName(3, "Permissions", TextAnchor.MiddleLeft);
			foreach (var perm in permission.GetPermissions().Where(x => x.StartsWith(plugin.Name.ToLower())))
			{
				var isInherited = false;
				var list = "";

				foreach (var group in permission.GetUserGroups(player.UserIDString))
					if (permission.GroupHasPermission(group, perm))
					{
						isInherited = true;
						list += $"<b>{group}</b>, ";
					}

				perms.AddRow(3, new Tab.OptionButton($"{perm}", instance5 =>
				{
					if (permission.UserHasPermission(player.UserIDString, perm))
						permission.RevokeUserPermission(player.UserIDString, perm);
					else permission.GrantUserPermission(player.UserIDString, perm, plugin);
				}, type: (_instance) => isInherited ? Tab.OptionButton.Types.Important : permission.UserHasPermission(player.UserIDString, perm) ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));

				if (isInherited)
				{
					perms.AddText(3, $"Inherited by the following groups: {list.TrimEnd(',', ' ')}", 8, "1 1 1 0.6", TextAnchor.UpperLeft, CUI.Handler.FontTypes.RobotoCondensedRegular);
				}
			}

		}
	}
	public class PlayersTab
	{
		internal static AdminModule Admin => GetModule<AdminModule>();

		public static Tab Get()
		{
			var players = new Tab("players", "Players", Community.Runtime.CorePlugin, (instance, tab) =>
			{
				tab.ClearColumn(1);
				RefreshPlayers(tab, instance);
			});
			{
				AddInitial(players, null);
				RefreshPlayers(players, null);
			}
			players.AddColumn(1);

			return players;
		}

		public static void AddInitial(Tab tab, AdminPlayer ap)
		{
			tab.AddInput(0, "Search", () => ap?.GetStorage<string>("playerfilter"), (ap2, args) => { ap2.SetStorage("playerfilter", args.ToString(" ")); RefreshPlayers(tab, ap2); });
			tab.AddName(0, "Player List", TextAnchor.MiddleLeft);
		}
		public static void RefreshPlayers(Tab tab, AdminPlayer ap)
		{
			tab.ClearColumn(0);

			AddInitial(tab, ap);

			foreach (var player in BasePlayer.allPlayerList.Where(x => x.userID.IsSteamId()))
			{
				AddPlayer(tab, ap, player);
			}
		}
		public static void AddPlayer(Tab tab, AdminPlayer ap, BasePlayer player)
		{
			if (ap != null)
			{
				var filter = ap.GetStorage<string>("playerfilter");

				if (!string.IsNullOrEmpty(filter) && !(player.displayName.ToLower().Contains(filter.ToLower()) || player.UserIDString.Contains(filter))) return;
			}

			tab.AddButton(0, $"{player.displayName}", aap =>
			{
				ap.SetStorage("playerfilterpl", player);
				ShowInfo(tab, ap, player);
			}, aap => aap == null || !(aap.GetStorage<BasePlayer>("playerfilterpl") == player) ? Tab.OptionButton.Types.None : Tab.OptionButton.Types.Selected);
		}
		public static void ShowInfo(Tab tab, AdminPlayer aap, BasePlayer player)
		{
			tab.ClearColumn(1);

			tab.AddName(1, $"Player Information", TextAnchor.MiddleLeft);
			tab.AddInput(1, "Name", () => player.displayName, null);
			tab.AddInput(1, "Steam ID", () => player.UserIDString, null);
			tab.AddInput(1, "Net ID", () => $"{player.net?.ID}", null);
			try
			{
				var position = player.transform.position;
				tab.AddInput(1, "Position", () => $"{position.x:0.0}x {position.y:0.0}y {position.z:0.0}z", null);
			}
			catch { }

			tab.AddName(1, $"Permissions", TextAnchor.MiddleLeft);
			{
				tab.AddButton(1, "View Permissions", ap =>
				{
					var perms = Admin.FindTab("permissions");
					var permission = Community.Runtime.CorePlugin.permission;
					Admin.SetTab(ap.Player, "permissions");

					ap.SetStorage("player", player);
					PermissionsTab.GeneratePlayers(perms, permission, ap);
					PermissionsTab.GeneratePlugins(perms, ap, permission, ap.Player);
				}, (ap) => Tab.OptionButton.Types.Important);
			}

			if (aap == null || aap.Player != player)
			{
				tab.AddName(1, $"Actions", TextAnchor.MiddleLeft);

				tab.AddButton(1, "Teleport", ap => { ap.Player.Teleport(player); }, (ap) => Tab.OptionButton.Types.Warned);
				tab.AddButton(1, "Teleport to me", ap => { player.Teleport(ap.Player); }, (ap) => Tab.OptionButton.Types.Warned);
			}
			else
			{
				tab.AddText(1, "This is you.", 8, "1 1 1 0.5", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedBold);
			}
		}
	}
	public class EntitiesTab
	{
		internal static float HurtHealAmount = 10f;
		internal static int EntityCount = 0;
		internal static string Filter;
		internal static float Range;
		internal static RustPlugin Core = Community.Runtime.CorePlugin;
		internal static AdminModule Admin = GetModule<AdminModule>();
		internal static AdminPlayer LastContainerLooter;

		public static Tab Get()
		{
			Range = World.Size;

			var tab = new Tab("entities", "Entities", Community.Runtime.CorePlugin, (ap, tab2) => { tab2.ClearColumn(1); DrawEntities(tab2, ap); });
			tab.AddColumn(0);
			tab.AddColumn(1);

			DrawEntities(tab);

			return tab;
		}

		internal static void DrawEntities(Tab tab, AdminPlayer ap3 = null)
		{
			tab.ClearColumn(0);

			tab.AddInputButton(0, "Search Entity", 0.3f,
				new Tab.OptionInput(null, () => Filter, 0, false, (ap, args) => { Filter = args.ToString(" "); DrawEntities(tab); }),
				new Tab.OptionButton($"Refresh", ap => { DrawEntities(tab); }));

			var pool = Facepunch.Pool.GetList<BaseEntity>();
			EntityCount = 0;

			using var map = Entities.Get<BaseEntity>(true);
			map.Each(entity =>
			{
				pool.Add(entity);
				EntityCount++;
			}, entity => entity != null && entity.transform != null && entity.transform.position != Vector3.zero && (string.IsNullOrEmpty(Filter) || entity.name.Contains(Filter) || entity.GetType().Name == Filter) && (Range != -1 && ap3 != null ? Vector3.Distance(ap3.Player.transform.position, entity.transform.position) <= Range : true));

			tab.AddRange(0, "Range", 0, World.Size, () => Range, (ap, value) => { Range = value; DrawEntities(tab, ap); }, () => $"{Range:0.0}m");
			tab.AddName(0, $"Entities  ({EntityCount:n0})", TextAnchor.MiddleLeft);

			foreach (var entity in pool)
			{
				tab.AddButton(0, $"{entity}", ap =>
				{
					var existent = ap.GetStorage<BaseEntity>("selectedent");
					if (existent != null && existent == entity)
					{
						ap.ClearStorage("selectedent");
						tab.ClearColumn(1);
					}
					else
					{
						DrawEntitySettings(tab, entity, 1, ap);
						ap.SetStorage("selectedent", entity);
					}
				}, ap => ap.GetStorage<BaseEntity>("selectedent") == entity ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None);
			}

			if (EntityCount == 0)
			{
				tab.AddText(0, "No entities found with that filter", 9, "1 1 1 0.2", TextAnchor.MiddleCenter, CUI.Handler.FontTypes.RobotoCondensedRegular);
			}
		}
		internal static void DrawEntitySettings(Tab tab, BaseEntity entity, int column = 1, AdminPlayer ap3 = null)
		{
			tab.ClearColumn(column);

			if (column != 1) tab.AddButton(column, "<", ap => { DrawEntities(tab, ap); DrawEntitySettings(tab, entity, 1, ap); }, ap => Tab.OptionButton.Types.Warned);

			if (entity != null && !entity.IsDestroyed)
			{
				var player = entity as BasePlayer;
				var owner = BasePlayer.FindByID(entity.OwnerID);

				if (player == null)
				{
					tab.AddButtonArray(column,
						new Tab.OptionButton("Kill", ap =>
						{
							entity.Kill();
							DrawEntities(tab, ap);
							tab.ClearColumn(column);
						}, ap => Tab.OptionButton.Types.Important),
						new Tab.OptionButton("Kill (Gibbed)", ap =>
						{
							entity.Kill(BaseNetworkable.DestroyMode.Gib);
							DrawEntities(tab, ap);
							tab.ClearColumn(column);
						}));
				}

				tab.AddInput(column, "Id", () => $"{entity.net.ID} [{entity.GetType().FullName}]", null);
				tab.AddInput(column, "Name", () => $"{entity.ShortPrefabName}", null);
				tab.AddInputButton(column, "Owner", 0.3f,
					new Tab.OptionInput(null, () => $"{(entity.OwnerID.IsSteamId() ? $"{BasePlayer.FindByID(entity.OwnerID).displayName}" : "None")}", 0, true, null),
					new Tab.OptionButton("Select", ap =>
					{
						if (owner == null) return;

						ap.SetStorage("selectedent", owner);
						DrawEntities(tab, ap);
						DrawEntitySettings(tab, owner, 1, ap);
					}, ap => owner == null ? Tab.OptionButton.Types.None : Tab.OptionButton.Types.Selected));
				tab.AddInput(column, "Prefab", () => $"{entity.PrefabName}", null);
				tab.AddInput(column, "Flags", () => entity.flags == 0 ? "None" : $"{entity.flags}", null);
				tab.AddButton(column, "Edit Flags", ap => { DrawEntitySettings(tab, entity, 0, ap); DrawEntityFlags(tab, entity, 1); });
				tab.AddInput(column, "Position", () => $"{entity.transform.position}", null);
				tab.AddInput(column, "Rotation", () => $"{entity.transform.rotation}", null);
				tab.AddButtonArray(column,
					new Tab.OptionButton("TeleportTo", ap => { ap.Player.Teleport(entity.transform.position); }),
					new Tab.OptionButton("Teleport2Me", ap => { entity.transform.position = ap.Player.transform.position; entity.SendNetworkUpdateImmediate(); }));

				if (entity is StorageContainer storage)
				{
					tab.AddButton(column, "Loot Container", ap =>
					{
						LastContainerLooter = ap;

						ap.SetStorage("lootedent", entity);
						Admin.Subscribe("OnEntityVisibilityCheck");
						Admin.Subscribe("OnEntityDistanceCheck");

						Core.timer.In(0.2f, () => Admin.Close(ap.Player));
						Core.timer.In(0.5f, () =>
						{
							SendEntityToPlayer(ap.Player, entity);

							ap.Player.inventory.loot.Clear();
							ap.Player.inventory.loot.PositionChecks = false;
							ap.Player.inventory.loot.entitySource = storage;
							ap.Player.inventory.loot.itemSource = null;
							ap.Player.inventory.loot.AddContainer(storage.inventory);
							ap.Player.inventory.loot.MarkDirty();
							ap.Player.inventory.loot.SendImmediate();

							ap.Player.ClientRPCPlayer(null, ap.Player, "RPC_OpenLootPanel", storage.panelName);
						});
					});
				}

				if (entity is BasePlayer)
				{
					tab.AddButtonArray(column,
						new Tab.OptionButton("Loot", ap =>
						{
							LastContainerLooter = ap;
							ap.SetStorage("lootedent", entity);
							SendEntityToPlayer(ap.Player, entity);

							Core.timer.In(0.2f, () => Admin.Close(ap.Player));
							Core.timer.In(0.5f, () =>
							{	
								SendEntityToPlayer(ap.Player, entity);

								ap.Player.inventory.loot.Clear();
								ap.Player.inventory.loot.PositionChecks = false;
								ap.Player.inventory.loot.entitySource = RelationshipManager.ServerInstance;
								ap.Player.inventory.loot.itemSource = null;
								ap.Player.inventory.loot.AddContainer(player.inventory.containerMain);
								ap.Player.inventory.loot.AddContainer(player.inventory.containerWear);
								ap.Player.inventory.loot.AddContainer(player.inventory.containerBelt);
								ap.Player.inventory.loot.MarkDirty();
								ap.Player.inventory.loot.SendImmediate();

								ap.Player.ClientRPCPlayer(null, ap.Player, "RPC_OpenLootPanel", "player_corpse");
							});
						}),
						new Tab.OptionButton("Respawn", ap =>
						{
							player.Hurt(player.MaxHealth()); player.Respawn(); player.EndSleeping();
						}));
					tab.AddInput(column, "Personal Message", null, (ap, args) => { player.ChatMessage($"[{ap.Player.displayName}]: {args.ToString(" ")}"); });
				}

				if (entity.parentEntity.IsValid(true)) tab.AddButton(column, $"Parent: {entity.parentEntity.Get(true)}", ap => { ap.SetStorage("selectedent", entity.parentEntity.Get(true)); DrawEntities(tab, ap); DrawEntitySettings(tab, entity.parentEntity.Get(true), 1, ap); });

				if (entity.children.Count > 0)
				{
					tab.AddName(column, "Children", TextAnchor.MiddleLeft);
					foreach (var child in entity.children)
					{
						tab.AddButton(column, $"{child}", ap => { ap.SetStorage("selectedent", child); DrawEntities(tab, ap); DrawEntitySettings(tab, child, 1, ap); });
					}
				}

				switch (entity)
				{
					case CCTV_RC cctv:
						{
							tab.AddName(column, "CCTV", TextAnchor.MiddleLeft);
							tab.AddInput(column, "Identifier", () => cctv.GetIdentifier(), (ap, args) => { cctv.UpdateIdentifier(args.ToString(""), true); });
							tab.AddButton(column, "View CCTV", ap =>
							{
								Core.timer.In(0.1f, () => { Admin.Close(ap.Player); ap.SetStorage("wasviewingcam", true); });
								Core.timer.In(0.3f, () =>
								{
									Admin.Subscribe("OnEntityDismounted");
									Admin.Subscribe("CanDismountEntity");

									var station = GameManager.server.CreateEntity("assets/prefabs/deployable/computerstation/computerstation.deployed.prefab", ap.Player.transform.position) as ComputerStation;
									station.SendControlBookmarks(ap.Player);
									station.Spawn();
									station.checkPlayerLosOnMount = false;
									station.legacyDismount = true;

									station.MountPlayer(ap.Player);
									ViewCamera(ap.Player, station, cctv);
								});
							});
							break;
						}
					case CodeLock codeLock:
						{
							tab.AddName(column, "Code Lock", TextAnchor.MiddleLeft);
							tab.AddInput(column, "Code", () => codeLock.code, (ap, args) => { var code = args.ToString(" "); foreach (var character in code) if (char.IsLetter(character)) return; codeLock.code = StringEx.Truncate(code, 4); });
							break;
						}
					case MiniCopter minicopter:
						{
							tab.AddName(column, "Minicopter", TextAnchor.MiddleLeft);
							tab.AddButton(column, "Open Fuel", ap => { LastContainerLooter = ap; Core.timer.In(0.2f, () => Admin.Close(ap.Player)); Core.timer.In(0.5f, () => { minicopter.engineController.FuelSystem.GetFuelContainer().PlayerOpenLoot(ap.Player, doPositionChecks: false); }); });
							break;
						}
				}
				if (entity is BaseCombatEntity combat)
				{
					tab.AddName(column, "Combat", TextAnchor.MiddleLeft);
					tab.AddRange(column, "Health", 0, combat.MaxHealth(), () => combat.health, (ap, value) => combat.SetHealth(value), () => $"{combat.health:0}");

					if (entity is BasePlayer)
					{
						tab.AddRange(column, "Thirst", 0, player.metabolism.hydration.max, () => player.metabolism.hydration.value, (ap, value) => player.metabolism.hydration.SetValue(value), () => $"{player.metabolism.hydration.value:0}");
						tab.AddRange(column, "Hunger", 0, player.metabolism.calories.max, () => player.metabolism.calories.value, (ap, value) => player.metabolism.calories.SetValue(value), () => $"{player.metabolism.calories.value:0}");
						tab.AddRange(column, "Radiation", 0, player.metabolism.radiation_poison.max, () => player.metabolism.radiation_poison.value, (ap, value) => player.metabolism.radiation_poison.SetValue(value), () => $"{player.metabolism.radiation_poison.value:0}");
					}

					tab.AddButtonArray(column,
						new Tab.OptionButton("Heal", ap => { combat.Heal(HurtHealAmount); }, ap => Tab.OptionButton.Types.Selected),
						new Tab.OptionButton("Hurt", ap => { combat.Hurt(HurtHealAmount); }, ap => Tab.OptionButton.Types.Warned));

					tab.AddRange(column, "Hurt/Heal Amount", 0, combat.MaxHealth(), () => HurtHealAmount.Clamp(0, combat.MaxHealth()), (ap, value) => HurtHealAmount = value, () => $"{HurtHealAmount:0}");
				}
			}
			else
			{
				DrawEntities(tab, ap3);
				tab.ClearColumn(1);
			}
		}
		internal static void DrawEntityFlags(Tab tab, BaseEntity entity, int column = 1)
		{
			var counter = 0;
			var currentButtons = Facepunch.Pool.GetList<Tab.OptionButton>();

			tab.ClearColumn(column);

			tab.AddName(column, "Entity Flags", TextAnchor.MiddleLeft);
			foreach (var flag in Enum.GetNames(typeof(BaseEntity.Flags)).OrderBy(x => x))
			{
				var flagValue = (BaseEntity.Flags)Enum.Parse(typeof(BaseEntity.Flags), flag);
				var hasFlag = entity.HasFlag(flagValue);

				currentButtons.Add(new Tab.OptionButton(flag, ap => { entity.SetFlag(flagValue, !hasFlag); DrawEntitySettings(tab, entity, 0); DrawEntityFlags(tab, entity, column); }, ap => hasFlag ? Tab.OptionButton.Types.Selected : Tab.OptionButton.Types.None));
				counter++;

				if (counter >= 5)
				{
					tab.AddButtonArray(column, currentButtons.ToArray());
					currentButtons.Clear();
					counter = 0;
				}
			}

			Facepunch.Pool.FreeList(ref currentButtons);
		}

		internal static void ViewCamera(BasePlayer player, ComputerStation station, CCTV_RC camera)
		{
			player.net.SwitchSecondaryGroup(camera.net.group);
			station.currentlyControllingEnt.uid = camera.net.ID;
			station.currentPlayerID = player.userID;
			var b = camera.InitializeControl(new CameraViewerId(station.currentPlayerID, 0L));
			station.SetFlag(BaseEntity.Flags.Reserved2, b, recursive: false, networkupdate: false);
			station.SendNetworkUpdateImmediate();
			station.SendControlBookmarks(player);
		}
		internal static void SendEntityToPlayer(BasePlayer player, BaseEntity entity)
		{
			var connection = player.Connection;

			if (connection == null)
			{
				return;
			}

			var netWrite = Network.Net.sv.StartWrite();

			if (netWrite == null)
			{
				return;
			}

			++connection.validate.entityUpdates;

			netWrite.PacketID(Network.Message.Type.Entities);
			netWrite.UInt32(connection.validate.entityUpdates);

			entity.ToStreamForNetwork((Stream)netWrite, new BaseNetworkable.SaveInfo() { forConnection = connection, forDisk = false });
			netWrite.Send(new SendInfo(connection));
		}
	}
	public class ModulesTab
	{
		public static Tab Get()
		{
			var tab = new Tab("modules", "Modules", Community.Runtime.CorePlugin);
			tab.AddColumn(0);
			tab.AddColumn(1);

			foreach (var hookable in Community.Runtime.ModuleProcessor.Modules)
			{
				if (hookable is BaseModule module)
				{
					tab.AddButton(0, $"{hookable.Name}", ap =>
					{
						DrawModuleSettings(tab, module);
					});
				}
			}

			return tab;
		}

		internal static void DrawModuleSettings(Tab tab, BaseModule module)
		{
			tab.ClearColumn(1);

			var carbonModule = module.GetType();
			var config = carbonModule.GetProperty("ConfigInstance").GetValue(module);
			var configEnabled = config.GetType().GetProperty("Enabled");
			var load = config.GetType().GetMethod("Load");
			var isEnabled = (bool)configEnabled?.GetValue(config);
			tab.AddInput(1, "Name", () => module.Name, null);
			tab.AddToggle(1, "Enabled", ap2 => { carbonModule.GetMethod("SetEnabled").Invoke(module, new object[] { !isEnabled }); DrawModuleSettings(tab, module); }, ap2 => isEnabled);
			tab.AddButton(1, "Reload", ap => { load.Invoke(module, null); });
		}
	}

	#endregion
}

public class AdminConfig
{
	public string OpenCommand = "cadmin";
	public int MinimumAuthLevel = 2;
}
public class AdminData
{

}
