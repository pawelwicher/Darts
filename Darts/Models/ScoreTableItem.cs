using System;

namespace Darts.Models
{
    public class ScoreTableItem
    {
        public int Rank { get; set; }
        public string Player { get; set; }
        public int Games { get; set; }
        public int Wins { get; set; }
        public int WinsAway { get; set; }
        public int Losses { get; set; }
        public string Run { get; set; }
        public string ScoresRun { get; set; }
    }
}