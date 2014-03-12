using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace RpgLevels
{
    public class Player
    {
        public int ID { get; set; }
        public string Name { get; set; }

        public byte Level { get; set; }
        public int Experience { get; set; }
        public int NextLevel { get; set; }
        public int SkillPoints { get; set; }

        public int Damage { get; set; }
        public int DamagePercent { get; set; }
        public int Defense { get; set; }
        public int DefensePercent { get; set; }
        public int HealOnHit { get; set; }
        public int LifeLeech { get; set; }
        public int Crit { get; set; }

        public Player(int id, string name)
        {
            ID = id;
            Name = name;
            Level = 1;
            NextLevel = 140;
        }

        public void OnMobKill(int hp)
        {
            GainExperience(hp);
        }

        public void GainExperience(int experience)
        {
            Experience += experience;
            if (Level < 99 && Experience >= NextLevel)
                LevelUp();

            if (Experience > 99999999)
                Experience = 99999999;
        }

        public void LevelUp()
        {
            if(Level++ == 99)
                Level = 99;

            if (Level == 99)
                NextLevel = 0;
            else
                NextLevel += (int)(Math.Pow(Level, 2) * 7.4 * 20);

            SkillPoints++;

            TShock.Players[ID].SendMessage(string.Format("You have leveled up to {0}! Type /level to up stats.", Level), Color.LightGreen);
        }
    }
}
