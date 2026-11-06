using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Plays many fights and whole levels with bots and logs the results, so difficulty can be tuned from the Inspector.
// The rules below mirror FightPhase.Update/SwapRoutine/ApplyHits and FishingGame.AddMiss: if those change, change these too.
public static class FightSimulator
{
    const int MaxSwaps = 200;
    // The hold bar sinks in real time; assume a player spends this long per swap (thinking + cascade animations).
    const float SwapSeconds = 1.6f, MissSeconds = 10;

    [MenuItem("FishingJam/Mô phỏng độ khó")]
    static void Run()
    {
        var game = Object.FindObjectOfType<FishingGame>();
        if (game) Debug.Log(Report(game, 100));
    }

    [MenuItem("FishingJam/Mô phỏng cả màn")]
    static void RunLevels()
    {
        var game = Object.FindObjectOfType<FishingGame>();
        if (game) Debug.Log(LevelReport(game, 30));
    }

    // One fight per run on a fresh board: careful bot / random bot.
    public static string Report(FishingGame game, int runs)
    {
        var sb = new StringBuilder($"Mô phỏng {runs} trận mỗi loài cá (khéo / bừa)\n");
        sb.AppendLine("Cá | Thắng % | Lượt đổi TB | Giây TB");
        foreach (var s in game.species)
        {
            var smart = Fights(game, s, 1, runs);
            var random = Fights(game, s, 0, runs);
            sb.AppendLine($"{s.name} | {smart.win:P0} / {random.win:P0} | {smart.swaps:F1} / {random.swaps:F1} | " +
                          $"{smart.swaps * SwapSeconds:F0} / {random.swaps * SwapSeconds:F0}");
        }
        return sb.ToString();
    }

    static (float win, float swaps) Fights(FishingGame game, FishingGame.Species s, float skill, int runs)
    {
        int wins = 0, swaps = 0;
        for (int i = 0; i < runs; i++)
        {
            var r = PlayOne(game, s, new SwapBoard(FightPhase.Kinds.ToList()), skill);
            swaps += r.swaps;
            if (r.won) wins++;
        }
        return ((float)wins / runs, (float)swaps / runs);
    }

    // One fight on the given board. skill: how often the bot plays the best-looking swap instead of a random one.
    static (bool won, int swaps) PlayOne(FishingGame game, FishingGame.Species s, SwapBoard board, float skill)
    {
        FightPhase.Settle(board);
        float catchBar = 0, hold = 1;
        int swaps = 0;
        bool struggling = false;
        float phaseLeft = 1 + FightPhase.PhaseLength(false);

        while (swaps < MaxSwaps)
        {
            var moves = board.Moves();
            if (moves.Count == 0) { board.Shuffle(); continue; }
            var move = Random.value < skill
                ? moves.OrderByDescending(m => FightPhase.MoveValue(game, s, board, m, struggling, catchBar, hold)).First()
                : moves[Random.Range(0, moves.Count)];
            swaps++;

            // one swap's worth of real time: the hold sinks and the fish may switch between struggling and tired
            hold -= s.drain * FightPhase.DrainMult(struggling) * SwapSeconds;
            if ((phaseLeft -= SwapSeconds) <= 0)
            {
                struggling = !struggling;
                phaseLeft = FightPhase.PhaseLength(struggling);
                if (struggling && s.lunges) hold -= s.yank;
                if (struggling) board.ApplySkill(s.skill);
                else board.Calm();
            }
            if (hold <= 0) return (false, swaps);

            bool star = board.IsStarSwap(move.a, move.b);
            board.Swap(move.a, move.b);
            var hits = star ? board.StarBlast(move.a, move.b) : board.Resolve(new[] { move.a, move.b });
            int step = 0;
            while (hits.Count > 0)
            {
                step++;
                foreach (var h in hits)
                {
                    var e = FightPhase.Effect(game.GetTile(h.type), h.amount, s, struggling, catchBar, step);
                    catchBar += e.reel;
                    hold = Mathf.Min(1, hold + e.hold);
                }
                board.Collapse();
                if (hold <= 0 && catchBar < 1) return (false, swaps);
                hits = board.Resolve(null);
            }
            if (catchBar >= 1) return (true, swaps);
        }
        return (false, swaps);
    }

    // ---- whole levels ----

    // A player profile: seconds to steer the hook onto a wanted fish, how often an unwanted fish grabs it first, fight skill.
    struct Player { public float castSeconds, wrongFish, skill; }

    static readonly Player[] Players =
    {
        new Player { castSeconds = 6, wrongFish = 0.1f, skill = 0.9f },  // careful
        new Player { castSeconds = 11, wrongFish = 0.3f, skill = 0.5f }, // clumsy
    };

    // Plays every level start to end on its shared board; a wrong fish or a lost fight costs MissSeconds and a retry.
    // Suggests a level time where a careful player keeps about 45% of the clock (two time stars need 40%)
    // and a clumsy one just makes it.
    public static string LevelReport(FishingGame game, int runs)
    {
        var sb = new StringBuilder($"Mô phỏng {runs} lần mỗi màn (khéo / vụng)\n");
        sb.AppendLine("Màn | Giờ hiện tại | Kịp giờ % | Giây dùng TB | Hụt TB | Giờ gợi ý");
        for (int i = 0; i < game.levels.Count; i++)
        {
            var level = game.levels[i];
            var res = Players.Select(p => PlayLevel(game, level, p, runs)).ToArray();
            float suggest = Mathf.Ceil(Mathf.Max(res[0].seconds / 0.55f, res[1].seconds * 1.05f) / 10) * 10;
            sb.AppendLine($"{i + 1}. {level.name} | {level.time:0} | {res[0].inTime:P0} / {res[1].inTime:P0} | " +
                          $"{res[0].seconds:F0} / {res[1].seconds:F0} | {res[0].misses:F1} / {res[1].misses:F1} | {suggest:0}");
        }
        return sb.ToString();
    }

    static (float inTime, float seconds, float misses) PlayLevel(FishingGame game, FishingGame.Level level, Player p, int runs)
    {
        int inTime = 0;
        float seconds = 0, misses = 0;
        // every fish bites, so a pond full of unwanted fish catches the hook more often (half decoys = the profile's rate)
        float decoys = level.pond.Length == 0 ? 0 : level.pond.Count(f => level.need.All(n => n.id != f.species)) / (float)level.pond.Length;
        float wrongFish = Mathf.Min(0.6f, p.wrongFish * (0.5f + decoys));
        for (int r = 0; r < runs; r++)
        {
            var board = new SwapBoard(FightPhase.Kinds.ToList());
            float t = 0;
            foreach (var need in level.need)
            {
                var s = game.GetFish(need.id);
                for (int n = 0; n < need.count && t < 1000;)
                {
                    t += p.castSeconds;
                    if (Random.value < wrongFish) { t += MissSeconds; misses++; continue; }
                    var fight = PlayOne(game, s, board, p.skill);
                    t += fight.swaps * SwapSeconds;
                    if (fight.won) n++;
                    else { t += MissSeconds; misses++; }
                }
            }
            if (t <= level.time) inTime++;
            seconds += t;
        }
        return ((float)inTime / runs, seconds / runs, misses / runs);
    }
}
