using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerrariaApi.Server;
using System.Reflection;
using Terraria;
using TShockAPI;
using System.IO;

namespace RpgLevels
{
    [ApiVersion(1,15)]
    public class RpgLevels : TerrariaPlugin
    {
        Random rnd = new Random();
        List<Player> players = new List<Player>();

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override string Name
        {
            get { return "RPG Levels"; }
        }

        public override string Author
        {
            get { return "Meth"; }
        }

        public override string Description
        {
            get { return "Allows experience to accumulate for mob kills and leveling up."; }
        }

        public RpgLevels(Main game) : base(game)
        {

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }
        }

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        void OnServerJoin(JoinEventArgs e)
        {
            try
            {
                // Get players by name to keep stats. Different slots will be assigned when rejoining.
                Player player = players.Where(p => p.Name == Main.player[e.Who].name).FirstOrDefault();
                if (player == null)
                {
                    players.Add(new Player(e.Who, Main.player[e.Who].name));
                }
                else
                {
                    player.ID = e.Who;
                }
            }
            catch (Exception ex)
            {
                Log.ConsoleError(ex.ToString());
            }
        }

        void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                int plr = e.Msg.whoAmI;
                using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    Player player = players.Where(p => p.ID == e.Msg.whoAmI).FirstOrDefault();
                    switch (e.MsgID)
                    {
                        case PacketTypes.PlayerDamage:
                            DoPlayerDamage(reader, player);
                            break;
                        case PacketTypes.EffectHeal:
                            DoPlayerHeal(reader, player);
                            break;
                        case PacketTypes.EffectMana:
                            DoPlayerMana(reader, player);
                            break;
                        case PacketTypes.NpcStrike:
                            DoNpcStrike(reader, player);
                            e.Handled = true;
                            break;
                    }
                }
            }
        }

        private void DoNpcStrike(BinaryReader reader, Player player)
        {
            Int16 npcID = reader.ReadInt16();
            Int16 dmg = reader.ReadInt16();
            float knockback = reader.ReadSingle();
            byte direction = reader.ReadByte();
            bool critical = reader.ReadBoolean();
            bool secondCrit = rnd.Next(1, 101) <= player.Crit;
            NPC npc = Main.npc[npcID];

            // Negate defense for +dmg
            int addDmg = player.Damage > 0 ? player.Damage + npc.defense / 2 : 0;
            // Make up additional damage for additional crit
            if (!critical && secondCrit)
                addDmg += dmg;
            double idmg = Main.CalculateDamage((int)addDmg+dmg, npc.ichor ? npc.defense - 20 : npc.defense);

            if (npc.life - idmg <= 0)
                player.OnMobKill(npc.lifeMax);

            if(player.HealOnHit > 0)
                TShock.Players[player.ID].Heal(player.HealOnHit);
            
            Main.npc[npcID].StrikeNPC((int)idmg,knockback,direction,critical | secondCrit,false);
            //TShock.Players[player.ID].SendInfoMessage(Main.npc[npcID].life.ToString());
            TShock.Players[player.ID].SendData(PacketTypes.NpcStrike, string.Empty, npcID, (float)(addDmg), (float)knockback, (float)direction, critical | secondCrit ? 1 : 0);
            NetMessage.SendData(24, -1, player.ID, "", npcID, (float)player.ID, 0f, 0f, 0);
            NetMessage.SendData(23, -1, -1, "", npcID, 0f, 0f, 0f, 0);
        }

        private static void DoPlayerHeal(BinaryReader reader, Player player)
        {
            byte playerID = reader.ReadByte();
            Int16 healAmount = reader.ReadInt16();

        }

        private static void DoPlayerMana(BinaryReader reader, Player player)
        {
            byte playerID = reader.ReadByte();
            Int16 manaAmount = reader.ReadInt16();

        }

        private static void DoPlayerDamage(BinaryReader reader, Player player)
        {
            byte playerID = reader.ReadByte();
            byte hitDirection = reader.ReadByte();
            Int16 damage = reader.ReadInt16();
            bool pvp = reader.ReadBoolean();
            bool crit = reader.ReadBoolean();

            // Since we can't override defense, we'll have to mitigate through healing :(
            if(player.Defense > 0)
                TShock.Players[player.ID].Heal(player.Defense);

                //Main.player[player.ID].Hurt(damage, hitDirection, pvp, true, null, crit);
                //NetMessage.SendData(26, -1, player.ID, string.Empty, player.ID, (float)hitDirection, (float)damage, pvp ? 1f : 0f, crit ? 1 : 0);
            
        }

        void OnChat(ServerChatEventArgs e)
        {
            string text = e.Text;
            if (e.Text.StartsWith("/"))
            {
                var sender = TShock.Players[e.Who];
                Player player = players.Where(p => p.ID == sender.Index).FirstOrDefault();
                //var sender = players.Where(p => p.Index == e.Who).FirstOrDefault();
                string[] arr = e.Text.Split(' ');
                switch (arr[0])
                {
                    case "/award":
                        player.SkillPoints = 999;
                        e.Handled = true;
                        break;
                    case "/exp":
                        sender.SendInfoMessage(string.Format("{0} Lv{1} Points: {2}", player.Name, player.Level, player.SkillPoints));
                        sender.SendInfoMessage(string.Format("Experience: {0:n0}/{1:n0} ({2:n2}%)", player.Experience, player.NextLevel, player.Experience * 100 / player.NextLevel));
                        e.Handled = true;
                        break;
                    case "/lvl":
                    case "/level":
                        if (arr.Length > 1)
                        {
                            byte points = 1;
                            try
                            {
                                if (arr.Length > 2)
                                    points = Byte.Parse(arr[2]);
                            }
                            catch { }
                            switch (arr[1].ToLower())
                            {
                                case "dmg":
                                case "damage":
                                    player.Damage += points;
                                    player.SkillPoints -= points;
                                    sender.SendMessage(string.Format("Your Damage has increased by {0}!",points), Color.Green);
                                    break;
                                case "def":
                                case "defense":
                                    player.Defense += points;
                                    player.SkillPoints -= points;
                                    sender.SendMessage(string.Format("Your Defense has increased by {0}!",points), Color.Green);
                                    break;
                                case "heal":
                                    player.HealOnHit += points;
                                    player.SkillPoints -= points;
                                    sender.SendMessage(string.Format("Your Heal on Hit has increased by {0}!",points),Color.Green);
                                    break;
                                case "crit":
                                    player.Crit += points;
                                    player.SkillPoints -= points;
                                    sender.SendMessage(string.Format("Your Crit Chance increased by {0}%!", points), Color.Green);
                                    break;
                                default:
                                    sender.SendErrorMessage("Available level up choices: dmg, def, heal");
                                    break;
                            }
                        }
                        else
                        {
                            sender.SendInfoMessage("Available level up bonuses: " + player.SkillPoints);
                            sender.SendInfoMessage("Type /lvl [choice] - dmg, def, heal");
                        }
                        e.Handled = true;
                        break;
                }
            }
        }
    }
}
