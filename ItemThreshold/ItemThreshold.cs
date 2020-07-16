using System;
using System.Collections.Generic;
using System.Timers;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Microsoft.Xna.Framework;
using TShockAPI.Hooks;

namespace ItemThreshold
{
    [ApiVersion(2, 1)]
    public class ItemThreshold : TerrariaPlugin
    {
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override string Name
        {
            get { return "ItemThreshold"; }
        }

        public override string Author
        {
            get { return "Simon311"; }
        }

        public override string Description
        {
            get { return "Adds threshold for dropping items."; }
        }

        public ItemThreshold(Main game)
            : base(game)
        {
            Order = 10;
        }

        static Timer Update = new Timer(1000);
        static int[] Thresholds = new int[256];
        static List<int> Exceeded = new List<int>();
        internal static int Threshold = 6;

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, GetData, -1);
            ServerApi.Hooks.GameInitialize.Register(this, Initialize, -10);
            GeneralHooks.ReloadEvent += Load;
        }
        private void Load(ReloadEventArgs e)
        {
            IConfig.Load();
            e.Player.SendSuccessMessage("[ItemThreshold] Config recarregada");
            Update = new Timer(IConfig.Config.msTimer);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, GetData);
                Update.Elapsed -= OnUpdate;
                Update.Stop();
            }
            base.Dispose(disposing);
        }

        private void Initialize(EventArgs e)
        {
            IConfig.Load();
            Update.Elapsed += OnUpdate;
            Update.Start();
        }

        private void GetData(GetDataEventArgs args)
        {
            if (IConfig.Config.Enabled)
            {
                if (args.MsgID == PacketTypes.ItemDrop)
                {
                    var num = args.Index;
                    var TPlayer = Main.player[args.Msg.whoAmI];
                    int ItemID = BitConverter.ToInt16(args.Msg.readBuffer, num);
                    if (ItemID == 400)
                    {
                        if (Exceeded.Contains(args.Msg.whoAmI))
                        {
                            args.Handled = true;
                            return;
                        }

                        if (Thresholds[args.Msg.whoAmI] > Threshold)
                        {
                            Exceeded.Add(args.Msg.whoAmI);
                            Thresholds[args.Msg.whoAmI] = 0;
                            args.Handled = true;
                        }
                        else Thresholds[args.Msg.whoAmI]++;
                    }
                }
                else if (args.MsgID == PacketTypes.UpdateItemDrop)
                {
                    var player = TShock.Players[args.Msg.whoAmI];
                    var user = TShock.Users.GetUserByName(player.Name);
                    if (user.Group != "banido")
                    {
                        TShock.Users.SetUserGroup(user, "banido");
                        player.tempGroup = null;
                        TShock.Log.ConsoleInfo("'Observador' modificou o grupo do usuário " + user.Name + " para: 'banido'.");
                        //player.SendSuccessMessage("Parabéns, você foi banido!");
                        string reason = "Cheating => #008";
                        string reason_extended = $"{reason}\nSe acha que isto é um erro, faça uma apelação em >\nhttps://discord.terrariabrasil.com.br";
                        if (!player.IsPortuguese)
                            reason_extended = $"{reason}\nIf you think this is a mistake, file an appeal at >\nhttps://discord.terrariabrasil.com.br";
                        TShock.Bans.AddBan(player.IP, user.Name, user.UUID, reason_extended, false, "Observador");
                        TShock.sendMessageDC(player.Name, $"ItemCheatDetection",
                               $"**O jogador:** está com anomalias em seus itens.\n" +
                               $"**Ação Realizada:** Jogador banido permanentemente.",
                               $"**Posição no Mapa X/Y**: {player.TileX}/{player.TileY}",
                               $"**Item selecionado:** {player.SelectedItem.Name} (Quantidade/Max: {player.SelectedItem.stack}/{player.SelectedItem.maxStack})", "capturados", TShock.DCColor(Color.Red));

                        TShock.sendMessageDC("", "", message: $"ItemCheatDetection\n" +
                               $"**O jogador:** está com anomalias em seus itens.\n" +
                               $"**Ação Realizada:** Jogador banido permanentemente.\n" +
                               $"**Posição no Mapa X/Y**: {player.TileX}/{player.TileY}\n" +
                               $"**Item selecionado:** {player.SelectedItem.Name} (Quantidade/Max: {player.SelectedItem.stack}/{player.SelectedItem.maxStack})", "log", logversion: 1);

                        if (IConfig.Config.KickOnCheat)
                        {
                            TShock.Utils.Kick(TShock.Players[args.Msg.whoAmI], reason_extended, true);
                            TShock.AllSendMessagev2($"[c/ff0000:'Observador (AntiCheat)'] baniu e expulsou [c/069740:{user.Name}]: [c/ffa500:'{reason}'].",
                                                    $"[c/ff0000:'Observer (AntiCheat)'] baniu e expulsou [c/069740:{user.Name}]: [c/ffa500:'{reason}'].", Color.Crimson);
                        }
                        else
                        {
                            TShock.AllSendMessagev2($"[c/ff0000:'Observador (AntiCheat)'] baniu [c/069740:{user.Name}]: [c/ffa500:'{reason}'].",
                                                    $"[c/ff0000:'Observer (AntiCheat)'] baniu [c/069740:{user.Name}]: [c/ffa500:'{reason}'].", Color.Crimson);
                        }
                        args.Handled = true;
                    }
                }
            }
        }

        private void OnUpdate(object sender, ElapsedEventArgs e)
        {
            if (IConfig.Config.Enabled)
            {
                if (Exceeded.Count > 0)
                {
                    var I = Exceeded.Count;
                    for (int i = 0; i < I; i++)
                    {
                        var Player = TShock.Players[Exceeded[i]];
                        if (Player == null || Player.TPlayer == null || !Player.TPlayer.active) continue;
                        if (!Player.TPlayer.dead || Player.TPlayer.statLife > 0)
                        {
                            string reason = "Item Spam";
                            Player.Disable(reason, DisableFlags.WriteToLogAndConsole);
                            if (IConfig.Config.KickOnSpam)
                            {
                                TShock.Utils.Kick(Player, reason, true);
                                TShock.AllSendMessagev2($"[c/ff0000:'Observador (AntiCheat)'] expulsou [c/069740:{Player.Name}]: [c/ffa500:'{reason}'].",
                                                        $"[c/ff0000:'Observer (AntiCheat)'] kicked [c/069740:{Player.Name}]: [c/ffa500:'{reason}'].", Color.Crimson);
                            }
                            else
                            {
                                TShock.AllSendMessagev2($"[c/ff0000:'Observador (AntiCheat)'] detectou uma anomalia com [c/069740:{Player.Name}]: [c/ffa500:'{reason}'].",
                                                        $"[c/ff0000:'Observer (AntiCheat)'] detected an anomaly with [c/069740:{Player.Name}]: [c/ffa500:'{reason}'].", Color.Crimson);
                            }
                        }
                    }
                    Exceeded.Clear();
                }

                Thresholds = new int[256];
            }
        }
    }
}
