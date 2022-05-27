using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Darts.Models;
using Darts.Utils;
using System.Threading.Tasks;

namespace Darts.Controllers
{
    public class DartsController : ApiController
    {
        private const string connectionString = @"User Id=sys;Password=system;Data Source=amsdev;Pooling=true;Enlist=false;Min Pool Size=4;DBA PRIVILEGE=sysdba;";

        private const string MATCH_TYPE_SEMI_FINAL_1 = "SEMI_FINAL_1";
        private const string MATCH_TYPE_SEMI_FINAL_2 = "SEMI_FINAL_2";
        private const string MATCH_TYPE_3RD = "3RD";
        private const string MATCH_TYPE_FINAL = "FINAL";
        private const string NO_NAME_PLAYER = "-";

        [HttpGet]
        public DartData GetDartData(int seasonNo)
        {
            var db = new DbEngine() { ConnectionString = connectionString };
            var i = 0;
            var sql = "";

            #region Query
            sql = @"select code, min_throws, max_points, active from dart_players";
            var players = db.Query(sql, new { code = "", min_throws = 0M, max_points = 0M, active = 0M });
            sql = @"select no season_no from dart_seasons order by no";
            var seasonsQueryResult = db.Query(sql, new { season_no = 0M });
            sql = @"select nvl(player_home_code, '-') home, nvl(player_away_code, '-') away, nvl(player_winner_code, '-') winner, season_no, nvl(game_date, to_date('1901-01-01')) game_date from dart_season_matches";
            var seasonMatchesQueryResult = db.Query(sql, new { home = "", away = "", winner = "", season_no = 0M, game_date = DateTime.MinValue });
            sql = @"select nvl(player_home, '-') home, nvl(player_away, '-') away, nvl(player_winner, '-') winner, season season_no, match_type from dart_season_playoffs";
            var seasonPlayoffsQueryResult = db.Query(sql, new { home = "", away = "", winner = "", season_no = 0M, match_type = "" });
            sql = @"select id, season_no, content, message_date from dart_board_messages order by id desc";
            var boardMessagesQueryResult = db.Query(sql, new { id = 0M, season_no = 0M, content = "", message_date = DateTime.MinValue });
            #endregion

            #region Seasons
            var seasons = (from s in seasonsQueryResult select new Season() { SeasonNo = Convert.ToInt32(s.season_no) }).ToList();

            if (seasonNo == 0)
            {
                seasonNo = (from s in seasons select s.SeasonNo).Max();
            }

            seasons.Add(new Season() { SeasonNo = seasons.Count + 1 });
            #endregion

            #region ScoreTable
            var scoreTable = new List<ScoreTableItem>();
            var seasonPlayers = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo select m.home).Union(
                                (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo select m.away)).Distinct();

            foreach (var player in seasonPlayers)
            {
                scoreTable.Add(new ScoreTableItem()
                {
                    Player = player,
                    Games = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo && m.winner != NO_NAME_PLAYER && (m.home == player || m.away == player) select m).Count(),
                    Wins = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo && m.winner == player select m).Count(),
                    WinsAway = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo && m.winner == player && m.away == player select m).Count(),
                    Losses = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo && m.winner != NO_NAME_PLAYER && m.winner != player && (m.home == player || m.away == player) select m).Count(),
                    Run = ""
                });
            }

            scoreTable = (from st in scoreTable orderby st.Wins descending, st.Games, st.WinsAway descending, st.Player select st).ToList();

            var scoreTableGroupped = from st in scoreTable
                                     group st by new { st.Wins, st.Games, st.WinsAway } into g
                                     orderby g.Key.Wins descending, g.Key.Games, g.Key.WinsAway descending
                                     select g;

            i = 1;
            scoreTable = new List<ScoreTableItem>();

            foreach (var item in scoreTableGroupped)
            {
                var count = item.Count();

                if (count == 2)
                {
                    var player1ScoreTableItem = item.ToList()[0];
                    var player2ScoreTableItem = item.ToList()[1];
                    var directMatchesWonByPlayer1Count = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo && ((m.home == player1ScoreTableItem.Player && m.away == player2ScoreTableItem.Player) || (m.home == player2ScoreTableItem.Player && m.away == player1ScoreTableItem.Player)) && m.winner == player1ScoreTableItem.Player select m).Count();
                    var directMatchesWonByPlayer2Count = (from m in seasonMatchesQueryResult where Convert.ToInt32(m.season_no) == seasonNo && ((m.home == player1ScoreTableItem.Player && m.away == player2ScoreTableItem.Player) || (m.home == player2ScoreTableItem.Player && m.away == player1ScoreTableItem.Player)) && m.winner == player2ScoreTableItem.Player select m).Count();

                    if (directMatchesWonByPlayer1Count == 2)
                    {
                        player1ScoreTableItem.Rank = i;
                        scoreTable.Add(player1ScoreTableItem);
                        i++;
                        player2ScoreTableItem.Rank = i;
                        scoreTable.Add(player2ScoreTableItem);
                        i++;
                    }
                    else if (directMatchesWonByPlayer2Count == 2)
                    {
                        player2ScoreTableItem.Rank = i;
                        scoreTable.Add(player2ScoreTableItem);
                        i++;
                        player1ScoreTableItem.Rank = i;
                        scoreTable.Add(player1ScoreTableItem);
                        i++;
                    }
                    else
                    {
                        item.ToList().ForEach(mt => mt.Rank = i);
                        scoreTable.AddRange(item);
                        i += item.Count();
                    }
                }
                else
                {
                    item.ToList().ForEach(mt => mt.Rank = i);
                    scoreTable.AddRange(item);
                    i += item.Count();
                }
            }

            foreach (var item in scoreTable)
            {
                item.Run = string.Join(" ", (from mr in seasonMatchesQueryResult
                                             where Convert.ToInt32(mr.season_no) == seasonNo &&
                                                   mr.game_date > new DateTime(1901, 1, 1) &&
                                                   mr.winner != NO_NAME_PLAYER && (mr.home == item.Player || mr.away == item.Player)
                                             orderby mr.game_date
                                             select (mr.winner == item.Player ? "W" : "L")).ToArray());
                item.ScoresRun = GetScoresRun(item.Run);
                item.Run = item.Run.Replace("W", @"<span class=""run_win"">●<span/>");
                item.Run = item.Run.Replace("L", @"<span class=""run_loss"">●<span/>");
            }
            #endregion

            #region PlayoffMatchScores
            var playoffMatchScores = new List<PlayoffMatchScore>();

            playoffMatchScores.AddRange(from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_1 select new PlayoffMatchScore() { Home = sp.home, Away = sp.away, Winner = sp.winner, Game = sp.match_type });
            playoffMatchScores.AddRange(from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_2 select new PlayoffMatchScore() { Home = sp.home, Away = sp.away, Winner = sp.winner, Game = sp.match_type });
            playoffMatchScores.AddRange(from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_3RD select new PlayoffMatchScore() { Home = sp.home, Away = sp.away, Winner = sp.winner, Game = sp.match_type });
            playoffMatchScores.AddRange(from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_FINAL select new PlayoffMatchScore() { Home = sp.home, Away = sp.away, Winner = sp.winner, Game = sp.match_type });
            #endregion

            #region PlayoffsResults
            var playoffsResults = new List<PlayoffResultItem>();
            var semiFinal1 = (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_1 select new PlayoffResultItem() { Game = MATCH_TYPE_SEMI_FINAL_1, Home = sp.home, Away = sp.away }).FirstOrDefault();
            var semiFinal2 = (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_2 select new PlayoffResultItem() { Game = MATCH_TYPE_SEMI_FINAL_2, Home = sp.home, Away = sp.away }).FirstOrDefault();
            var thirdPlace = (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_3RD select new PlayoffResultItem() { Game = MATCH_TYPE_3RD, Home = sp.home, Away = sp.away }).FirstOrDefault();
            var final = (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_FINAL select new PlayoffResultItem() { Game = MATCH_TYPE_FINAL, Home = sp.home, Away = sp.away }).FirstOrDefault();

            if (semiFinal1 != null)
            {
                semiFinal1.Score = string.Format("{0} - {1}", (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_1 && sp.winner == semiFinal1.Home && sp.winner != NO_NAME_PLAYER select sp).Count(), (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_1 && sp.winner == semiFinal1.Away && sp.winner != NO_NAME_PLAYER select sp).Count());
                playoffsResults.Add(semiFinal1);
            }

            if (semiFinal2 != null)
            {
                semiFinal2.Score = string.Format("{0} - {1}", (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_2 && sp.winner == semiFinal2.Home && sp.winner != NO_NAME_PLAYER select sp).Count(), (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_SEMI_FINAL_2 && sp.winner == semiFinal2.Away && sp.winner != NO_NAME_PLAYER select sp).Count());
                playoffsResults.Add(semiFinal2);
            }

            if (thirdPlace != null)
            {
                thirdPlace.Score = string.Format("{0} - {1}", (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_3RD && sp.winner == thirdPlace.Home && sp.winner != NO_NAME_PLAYER select sp).Count(), (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_3RD && sp.winner == thirdPlace.Away && sp.winner != NO_NAME_PLAYER select sp).Count());
                playoffsResults.Add(thirdPlace);
            }

            if (final != null)
            {
                final.Score = string.Format("{0} - {1}", (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_FINAL && sp.winner == final.Home && sp.winner != NO_NAME_PLAYER select sp).Count(), (from sp in seasonPlayoffsQueryResult where Convert.ToInt32(sp.season_no) == seasonNo && sp.match_type == MATCH_TYPE_FINAL && sp.winner == final.Away && sp.winner != NO_NAME_PLAYER select sp).Count());
                playoffsResults.Add(final);
            }
            #endregion

            #region MatchResults
            var matchResults = (from mr in seasonMatchesQueryResult
                                where Convert.ToInt32(mr.season_no) == seasonNo
                                orderby (mr.winner != NO_NAME_PLAYER ? "" : mr.winner), mr.game_date
                                select new MatchScore() { Home = mr.home, Away = mr.away, Winner = mr.winner }).ToList();
            #endregion

            #region HighScores
            var highScores = (from p in players select new HighScoreItem() { Player = p.code }).ToList();

            foreach (var item in highScores)
            {
                item.Seasons = (from mr in seasonMatchesQueryResult where mr.home == item.Player select mr.season_no).Distinct().Count();
                item.Games = (from mr in seasonMatchesQueryResult where (mr.home == item.Player || mr.away == item.Player) && mr.winner != NO_NAME_PLAYER select mr).Count() +
                             (from mr in seasonPlayoffsQueryResult where (mr.home == item.Player || mr.away == item.Player) && mr.winner != NO_NAME_PLAYER select mr).Count();
                item.Wins = (from mr in seasonMatchesQueryResult where (mr.home == item.Player || mr.away == item.Player) && mr.winner == item.Player select mr).Count() +
                            (from mr in seasonPlayoffsQueryResult where (mr.home == item.Player || mr.away == item.Player) && mr.winner == item.Player select mr).Count();
                item.Ratio = item.Games == 0 ? 0 : (int)(((decimal)item.Wins / (decimal)item.Games) * 100);
            }

            i = 0;
            highScores = (from hs in highScores orderby hs.Wins descending, hs.Games, hs.Player select hs).ToList();
            highScores.ForEach(hs => hs.Rank = ++i);
            #endregion

            #region MedalTable
            var medalTable = new List<MedalsTableItem>();
            var medalsTableItem = null as MedalsTableItem;

            var _3rdsAndFinals = from v in seasonPlayoffsQueryResult
                                 where v.home != NO_NAME_PLAYER && v.away != NO_NAME_PLAYER && (v.match_type == MATCH_TYPE_3RD || v.match_type == MATCH_TYPE_FINAL)
                                 group v by new { v.season_no, v.match_type } into g
                                 select g;

            foreach (var item in _3rdsAndFinals)
            {
                if (item.Key.match_type == MATCH_TYPE_3RD && item.Count() > 1)
                {
                    foreach (var w in (from v in item where v.winner != NO_NAME_PLAYER group v by v.winner))
                    {
                        if (w.Count() == 2)
                        {
                            var _3rd = w.Key;
                            medalsTableItem = (from mt in medalTable where mt.Player == _3rd select mt).FirstOrDefault();
                            if (medalsTableItem == null)
                            {
                                medalsTableItem = new MedalsTableItem();
                                medalTable.Add(medalsTableItem);
                            }
                            medalsTableItem.Player = w.Key;
                            medalsTableItem.BronzeCount++;
                        }
                    }
                }
                else if (item.Key.match_type == MATCH_TYPE_FINAL && item.Count() > 1)
                {
                    foreach (var w in (from v in item where v.winner != NO_NAME_PLAYER group v by v.winner))
                    {
                        if (w.Count() == 2)
                        {
                            var _1st = w.Key;
                            medalsTableItem = (from mt in medalTable where mt.Player == _1st select mt).FirstOrDefault();
                            if (medalsTableItem == null)
                            {
                                medalsTableItem = new MedalsTableItem();
                                medalTable.Add(medalsTableItem);
                            }
                            medalsTableItem.Player = _1st;
                            medalsTableItem.GoldCount++;

                            var _2nd = (from v in item where v.home == _1st select v.away).FirstOrDefault();
                            medalsTableItem = (from mt in medalTable where mt.Player == _2nd select mt).FirstOrDefault();
                            if (medalsTableItem == null)
                            {
                                medalsTableItem = new MedalsTableItem();
                                medalTable.Add(medalsTableItem);
                            }
                            medalsTableItem.Player = _2nd;
                            medalsTableItem.SilverCount++;
                        }
                    }
                }
            }

            medalTable = (from mt in medalTable
                          where mt.GoldCount > 0 || mt.SilverCount > 0 || mt.BronzeCount > 0
                          orderby mt.GoldCount descending, mt.SilverCount descending, mt.BronzeCount descending, mt.Player
                          select mt).ToList();

            var medalTableGroupped = from mt in medalTable
                                     group mt by new { mt.GoldCount, mt.SilverCount, mt.BronzeCount } into g
                                     orderby g.Key.GoldCount descending, g.Key.SilverCount descending, g.Key.BronzeCount descending
                                     select g;

            i = 1;
            medalTable = new List<MedalsTableItem>();

            foreach (var item in medalTableGroupped)
            {
                item.ToList().ForEach(mt => mt.Rank = i);
                medalTable.AddRange(item);
                i += item.Count();
            }
            #endregion

            #region BoardMessages
            var boardMessages = (from bm in boardMessagesQueryResult
                                 where Convert.ToInt32(bm.season_no) == seasonNo
                                 orderby bm.message_date descending
                                 select new BoardMessage() { Id = Convert.ToInt32(bm.id), Content = bm.content, DateString = bm.message_date.ToString("yyy-MM-dd HH:mm") }).ToList();
            #endregion

            return new DartData()
            {
                CurrentSeason = seasonNo,
                PlayedGames = (from mr in seasonMatchesQueryResult where Convert.ToInt32(mr.season_no) == seasonNo && mr.winner != NO_NAME_PLAYER select mr).Count(),
                AllGames = (from mr in seasonMatchesQueryResult where Convert.ToInt32(mr.season_no) == seasonNo select mr).Count(),
                Seasons = seasons,
                ScoreTable = scoreTable,
                PlayoffMatchScores = playoffMatchScores,
                PlayoffsResults = playoffsResults,
                MatchResults = matchResults,
                HighScores = highScores,
                MedalTable = medalTable,
                Players = (new List<string>() { NO_NAME_PLAYER }).Union((from st in scoreTable orderby st.Player select st.Player)).ToList(),
                BoardMessages = boardMessages
            };
        }

        [HttpGet]
        public int SetWinner(int seasonNo, string playerHome, string playerAway, string winner)
        {
            var db = new DbEngine() { ConnectionString = connectionString };
            var sql = @"select nvl(player_winner_code, '-') winner from dart_season_matches where season_no = {0} and player_home_code = '{1}' and player_away_code = '{2}'";

            if (db.Query(string.Format(sql, seasonNo, playerHome, playerAway), new { winner = "" }).First().winner != NO_NAME_PLAYER)
            {
                db.ExecuteNonQuery(string.Format("update dart_season_matches set player_winner_code = null, game_date = null where season_no = {0} and player_home_code = '{1}' and player_away_code = '{2}'", seasonNo, playerHome, playerAway));
            }
            else
            {
                db.ExecuteNonQuery(string.Format("update dart_season_matches set player_winner_code = '{3}', game_date = to_date('{4:yyyy-MM-dd HH:mm:ss}', 'yyyy-mm-dd hh24:mi:ss') where season_no = {0} and player_home_code = '{1}' and player_away_code = '{2}'", seasonNo, playerHome, playerAway, winner, DateTime.Now));

                SendNotifications("New Match Result", string.Format("Mecz {0} - {1} wygrał {2}.", playerHome, playerAway, winner));
            }

            return 0;
        }

        [HttpGet]
        public int SetPlayoffMatchWinner(int seasonNo, string matchType, string playerHome, string playerAway, string winner)
        {
            var db = new DbEngine() { ConnectionString = connectionString };
            var sql = @"select nvl(player_winner, '-') winner from dart_season_playoffs where season = {0} and match_type = '{1}' and player_home = '{2}' and player_away = '{3}'";

            if (db.Query(string.Format(sql, seasonNo, matchType, playerHome, playerAway), new { winner = "" }).First().winner != NO_NAME_PLAYER)
            {
                db.ExecuteNonQuery(string.Format("update dart_season_playoffs set player_winner = null where season = {0} and match_type = '{1}' and player_home = '{2}' and player_away = '{3}'", seasonNo, matchType, playerHome, playerAway));
            }
            else
            {
                db.ExecuteNonQuery(string.Format("update dart_season_playoffs set player_winner = '{4}' where season = {0} and match_type = '{1}' and player_home = '{2}' and player_away = '{3}'", seasonNo, matchType, playerHome, playerAway, winner));

                SendNotifications("New Playoff Result", string.Format("Mecz {0} - {1} wygrał {2}.", playerHome, playerAway, winner));
            }

            return 0;
        }

        [HttpPost]
        public void SubmitMessage(BoardMessage message)
        {
            var db = new DbEngine() { ConnectionString = connectionString };
            var sql = @"insert into dart_board_messages(id, season_no, content, message_date) values (dart_board_messages_sq.nextval, {0}, '{1}', to_date('{2:yyyy-MM-dd HH:mm}', 'yyyy-mm-dd hh24:mi'))";
            db.ExecuteNonQuery(string.Format(sql, message.SeasonNo, message.Content, DateTime.Now));

            SendNotifications("New Board Message", message.Content);
        }

        [NonAction]
        private void SendNotifications(string title, string message)
        {
            var db = new DbEngine() { ConnectionString = connectionString };
            var notificationClients = db.Query("select machine_name, port, rowid from dart_notifications", new { machine_name = "", port = 0M });

            Parallel.ForEach(notificationClients, (client) =>
            {
                var result = Notyfications.SendNotificationsToClient(client.machine_name, Convert.ToInt32(client.port), 100, Notyfications.Icon.INFO, title, message);

                if (!string.IsNullOrEmpty(result))
                {
                    Log(result);
                }
            });
        }

        [NonAction]
        private void Log(string content)
        {
            var db = new DbEngine() { ConnectionString = connectionString };
            var sql = @"insert into dart_logs(content, dt) values ('{0}', to_date('{1:yyyy-MM-dd HH:mm:ss}', 'yyyy-mm-dd hh24:mi:ss'))";
            db.ExecuteNonQuery(string.Format(sql, content, DateTime.Now));
        }

        [NonAction]
        private string GetScoresRun(string run)
        {
            if (!string.IsNullOrWhiteSpace(run))
            {
                var c = run[run.Length - 1];
                var count = 0;

                foreach (var chr in run.Replace(" ", "").Reverse())
                {
                    if (chr == c)
                    {
                        count++;
                    }
                    else
                    {
                        break;
                    }
                }

                return (c == 'W' ? "+" : "-") + count;
            }

            return "";
        }
    }
}