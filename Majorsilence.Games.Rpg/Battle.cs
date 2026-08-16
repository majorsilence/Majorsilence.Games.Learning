namespace Majorsilence.Games.Rpg;

public enum BattlePhase
{
    /// <summary>Waiting for the player to pick a command.</summary>
    Command,

    /// <summary>Waiting for the player to pick which monster to hit.</summary>
    Target,

    /// <summary>Showing what happened, one line per Confirm.</summary>
    Message,

    /// <summary>Nothing left to do; read Outcome and leave.</summary>
    Over
}

public enum BattleOutcome
{
    None,
    Victory,
    Defeat,
    Fled
}

public enum BattleCommand
{
    Fight,
    Run
}

/// <summary>
/// One fight, start to finish: whose turn it is, what the player picked, who hit
/// whom, and how it ended.
///
/// Pure logic - no SDL, no textures, no input. It is driven by four calls
/// (MoveCommand, MoveTarget, Confirm, Cancel) and read for display, which keeps
/// every formula in this file testable without opening a window. BattleScreen
/// draws it; RpgGame decides when one starts.
///
/// The round structure is the console-RPG one: the player commits an action,
/// then everyone still standing acts in agility order, then the round ends and
/// the player is asked again. Everything a round did is queued as messages and
/// paged through at the player's pace, so nothing happens off-screen.
/// </summary>
public class Battle
{
    /// <summary>1 in this many hits lands for double damage.</summary>
    private const int CriticalChance = 16;

    private readonly Random _random;
    private readonly Queue<string> _messages = new();
    private readonly Queue<Combatant> _turnQueue = new();
    private readonly List<Combatant> _monsters;

    /// <summary>Set when the round began with a failed escape: the hero's turn in it is spent, not swung.</summary>
    private bool _heroSkipsAttack;

    public Battle(Combatant hero, IEnumerable<Combatant> monsters, Random random)
    {
        Hero = hero;
        _monsters = monsters.ToList();
        _random = random;

        Say(_monsters.Count == 1
            ? $"{_monsters[0].Name} blocks the way!"
            : $"{_monsters.Count} foes block the way!");

        // Show the opening line straight away rather than waiting for the first
        // button press, which would otherwise open the fight on a blank panel.
        NextMessage();
    }

    public Combatant Hero { get; }
    public IReadOnlyList<Combatant> Monsters => _monsters;

    public BattlePhase Phase { get; private set; } = BattlePhase.Message;
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.None;

    /// <summary>The line currently on show, or "" when not in the Message phase.</summary>
    public string Message { get; private set; } = "";

    public BattleCommand Command { get; private set; } = BattleCommand.Fight;

    /// <summary>Which monster the cursor is on. Always points at a living one.</summary>
    public int TargetIndex { get; private set; }

    public int ExperienceEarned { get; private set; }

    /// <summary>Living monsters, in their original left-to-right order - what the screen draws and the cursor walks.</summary>
    public IEnumerable<Combatant> Living => _monsters.Where(m => m.IsAlive);

    public void MoveCommand(int delta)
    {
        if (Phase != BattlePhase.Command) return;
        Command = Command == BattleCommand.Fight ? BattleCommand.Run : BattleCommand.Fight;
    }

    public void MoveTarget(int delta)
    {
        if (Phase != BattlePhase.Target || delta == 0) return;

        // Step over the dead: a cursor that can rest on a corpse invites the
        // player to waste a turn on one.
        var step = Math.Sign(delta);
        for (var i = 0; i < _monsters.Count; i++)
        {
            TargetIndex = (TargetIndex + step + _monsters.Count) % _monsters.Count;
            if (_monsters[TargetIndex].IsAlive) return;
        }
    }

    public void Cancel()
    {
        if (Phase == BattlePhase.Target) Phase = BattlePhase.Command;
    }

    /// <summary>The one button that drives everything: commits a command, picks a target, or turns the page.</summary>
    public void Confirm()
    {
        switch (Phase)
        {
            case BattlePhase.Message:
                NextMessage();
                break;

            case BattlePhase.Command when Command == BattleCommand.Fight:
                SnapTargetToLiving();
                Phase = BattlePhase.Target;
                break;

            case BattlePhase.Command:
                ResolveRound(runningAway: true);
                break;

            case BattlePhase.Target:
                ResolveRound(runningAway: false);
                break;
        }
    }

    private void SnapTargetToLiving()
    {
        if (_monsters[TargetIndex].IsAlive) return;
        TargetIndex = _monsters.FindIndex(m => m.IsAlive);
        if (TargetIndex < 0) TargetIndex = 0;
    }

    private void ResolveRound(bool runningAway)
    {
        if (runningAway && TryRun())
        {
            NextMessage();
            return;
        }

        // The round is queued, not played out. Each turn runs only when the
        // player reaches it, so the health on screen always matches the line
        // being read - resolving the whole round up front would show a monster
        // already at zero while its attack was still being narrated.
        //
        // Order is recomputed each round rather than fixed at the start, so a
        // fight that kills the quick one changes who goes next.
        _heroSkipsAttack = runningAway;
        _turnQueue.Clear();
        foreach (var actor in TurnOrder()) _turnQueue.Enqueue(actor);

        NextMessage();
    }

    private IEnumerable<Combatant> TurnOrder()
    {
        var actors = new List<Combatant> { Hero };
        actors.AddRange(_monsters.Where(m => m.IsAlive));
        // Ties broken randomly so two equally quick combatants don't have a
        // fixed pecking order for the whole fight.
        return actors.OrderByDescending(a => a.Agility).ThenBy(_ => _random.Next());
    }

    private bool TryRun()
    {
        var fastest = _monsters.Where(m => m.IsAlive).Select(m => m.Agility).DefaultIfEmpty(0).Max();
        var chance = Math.Clamp(0.55 + (Hero.Agility - fastest) * 0.04, 0.15, 0.95);

        if (_random.NextDouble() >= chance)
        {
            Say("There's no getting away.");
            return false;
        }

        Say($"{Hero.Name} breaks off and runs.");
        Outcome = BattleOutcome.Fled;
        return true;
    }

    private void HeroAttacks()
    {
        var target = _monsters[TargetIndex];
        if (!target.IsAlive)
        {
            target = _monsters.FirstOrDefault(m => m.IsAlive) ?? target;
        }

        Strike(Hero, target, $"{Hero.Name} strikes the {target.Name}");
        if (!target.IsAlive) Say($"The {target.Name} goes down.");

        if (_monsters.All(m => !m.IsAlive))
        {
            ExperienceEarned = _monsters.Sum(m => m.Experience);
            Say($"The way is clear. {ExperienceEarned} experience.");
            Outcome = BattleOutcome.Victory;
        }
    }

    private void MonsterAttacks(Combatant monster)
    {
        Strike(monster, Hero, $"The {monster.Name} lunges");

        if (!Hero.IsAlive)
        {
            Say($"{Hero.Name} goes down in the ash.");
            Outcome = BattleOutcome.Defeat;
        }
    }

    private void Strike(Combatant attacker, Combatant defender, string opening)
    {
        if (_random.NextDouble() >= HitChance(attacker, defender))
        {
            Say($"{opening} - and misses.");
            return;
        }

        var damage = DamageFrom(attacker, defender);
        var critical = _random.Next(CriticalChance) == 0;
        if (critical) damage *= 2;

        defender.Health = Math.Max(0, defender.Health - damage);
        Say(critical
            ? $"{opening} - a telling blow, {damage} damage."
            : $"{opening} for {damage} damage.");
    }

    /// <summary>
    /// Attack power less half the target's defence, never below 1 - a fight
    /// always makes progress, however badly matched, so no encounter can
    /// deadlock with neither side able to hurt the other.
    /// </summary>
    public int DamageFrom(Combatant attacker, Combatant defender)
    {
        var baseDamage = Math.Max(1, attacker.Attack - defender.Defense / 2);
        // +/- 25%, so repeated hits on the same target don't read as a fixed number
        var spread = Math.Max(1, baseDamage / 4);
        return Math.Max(1, baseDamage + _random.Next(-spread, spread + 1));
    }

    /// <summary>Mostly hits; the agility gap moves it, but never to a certainty in either direction.</summary>
    public static double HitChance(Combatant attacker, Combatant defender) =>
        Math.Clamp(0.80 + (attacker.Agility - defender.Agility) * 0.02, 0.55, 0.97);

    private void Say(string line) => _messages.Enqueue(line);

    /// <summary>
    /// Shows the next line, running the next queued turn first if there is
    /// nothing left to say. This is the only place a turn is actually played, so
    /// what the player reads and what the numbers show can never disagree.
    /// </summary>
    private void NextMessage()
    {
        while (_messages.Count == 0 && _turnQueue.Count > 0 && Outcome == BattleOutcome.None)
        {
            var actor = _turnQueue.Dequeue();
            if (!actor.IsAlive) continue; // killed earlier this round; its turn is gone

            if (actor == Hero)
            {
                if (!_heroSkipsAttack) HeroAttacks();
            }
            else
            {
                MonsterAttacks(actor);
            }
        }

        if (_messages.Count > 0)
        {
            Message = _messages.Dequeue();
            Phase = BattlePhase.Message;
            return;
        }

        _turnQueue.Clear();
        Message = "";
        Phase = Outcome == BattleOutcome.None ? BattlePhase.Command : BattlePhase.Over;
    }
}
