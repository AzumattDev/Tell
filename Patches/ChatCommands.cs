using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;

namespace Tell.Patches;

public static class ChatCommands
{
    private static string tellChatPlaceholder = null!;
    private static bool tellChatActive => Chat.instance && Chat.instance.m_input.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>().text == tellChatPlaceholder;

    private static readonly List<Terminal.ConsoleCommand> terminalCommands = new();

    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    public class AddChatCommands
    {
        private static void Postfix()
        {
            tellChatPlaceholder = Localization.instance.Localize("$tell_chat_placeholder");

            terminalCommands.Clear();

            terminalCommands.Add(new Terminal.ConsoleCommand("t", "sends a private tell message to a player", (Terminal.ConsoleEvent)(args =>
            {
                if (Chat.instance == null)
                {
                    return;
                }

                if (args.FullLine.Length > 2)
                {
                    string trimmedLine = args.FullLine.Substring(2).TrimStart();
                    string targetPlayerName = "";
                    string message = "";

                    // Get a list of all player names.
                    var playerNames = ZNet.instance.m_players.Select(p => p.m_name).ToList();

                    // Try to match the largest substring starting from the beginning that matches a player name.
                    foreach (var name in playerNames.OrderByDescending(n => n.Length))
                    {
                        if (trimmedLine.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                        {
                            targetPlayerName = name;
                            SendMessageToGroup.LastPlayerName = name;
                            int nameLength = name.Length;
                            message = trimmedLine.Substring(nameLength).TrimStart();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(targetPlayerName))
                    {
                        args.Context.AddString(Localization.instance.Localize("$tell_chat_invalid_player_name"));
                        return;
                    }

                    long targetId = ZNet.instance.m_players.FirstOrDefault(p => string.Compare(targetPlayerName, p.m_name, StringComparison.OrdinalIgnoreCase) == 0).m_characterID.UserID;
                    if (targetId == 0)
                    {
                        args.Context.AddString(Localization.instance.Localize("$tell_chat_player_not_online", targetPlayerName));
                        return;
                    }

                    if (targetId != 0 && message.Length > 0)
                    {
                        ZRoutedRpc.instance.InvokeRoutedRPC(targetId, "Tell PrivateMessage", UserInfo.GetLocalUser(), message);
                    }
                }
                else
                {
                    ToggleTellChat(!tellChatActive);
                }
            }), optionsFetcher: () => ZNet.instance.m_players.Select(p => p.m_name).Where(n => n != Player.m_localPlayer.GetHoverName()).ToList()));
        }
    }

    public static void ToggleTellChat(bool active)
    {
        if (Chat.instance)
        {
            TextMeshProUGUI placeholder = Chat.instance.m_input.transform.Find("Text Area/Placeholder").GetComponent<TextMeshProUGUI>();
            if (active)
            {
                placeholder.text = tellChatPlaceholder;
                Localization.instance.textMeshStrings[placeholder] = tellChatPlaceholder;
            }
            else if (placeholder.text == tellChatPlaceholder)
            {
                placeholder.text = Localization.instance.Localize("$chat_entertext");
                Localization.instance.textMeshStrings[placeholder] = "$chat_entertext";
            }
        }
    }

    public static void UpdateAutoCompletion()
    {
        foreach (Terminal.ConsoleCommand command in terminalCommands)
        {
            command.m_tabOptions = null;
        }
    }

    [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
    public class AddGroupChat
    {
        private static void Postfix(Chat __instance)
        {
            int insertIndex = Math.Max(0, __instance.m_chatBuffer.Count - 5);
            __instance.m_chatBuffer.Insert(insertIndex, Localization.instance.Localize("$tell_chat_message_hint"));
            __instance.m_chatBuffer.Insert(insertIndex, Localization.instance.Localize("$tell_chat_toggle_hint"));
            __instance.UpdateChat();
        }
    }

    [HarmonyPatch(typeof(Chat), nameof(Chat.InputText))]
    public class SendMessageToGroup
    {
        internal static string? LastPlayerName = string.Empty;

        private static void Prefix(Chat __instance)
        {
            if (__instance.m_input.text.Length != 0 && tellChatActive && __instance.m_input.text[0] != '/')
            {
                __instance.m_input.text = $"/t {LastPlayerName}" + __instance.m_input.text;
            }
        }
    }
}