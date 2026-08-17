namespace Majorsilence.Games.Rpg;

public enum BattlePhase
{
    /// <summary>Waiting for a command for the party member currently being planned.</summary>
    Command,

    /// <summary>Waiting for a spell choice.</summary>
    Spell,

    /// <summary>Waiting for an item choice.</summary>
    Item,

    /// <summary>Waiting for which monster to hit.</summary>
    Target,

    /// <summary>Waiting for which companion to help.</summary>
    AllyTarget,

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
    Magic,
    Item,
    Run
}

/// <summary>
/// One fight, start to finish: what each character is told to do, who acts in
/// what order, and how it ended.
///
/// Pure logic - no SDL, no textures, no input. It is driven by four calls
/// (MoveCursor, Confirm, Cancel, and construction) and read for display, which
/// keeps every formula in this file testable without opening a window.
/// BattleScreen draws it; RpgGame decides when one starts.
///
/// The round has the console-RPG shape: orders are given to the whole party
/// first, then everyone - party and monsters together - acts in agility order.
/// So a fast monster can act before a character whose order you gave first, and
/// a character can be struck down before carrying out an order you already
/// issued. That is the tension the format is built on.
///
/// A round is queued rather than played out at once, and each turn runs at the
/// moment its message is reached, so the health on screen can never disagree
/// with the line being read.
/// </summary>
public class Battle
{
    /// <summary>1 in this many hits lands for double damage.</summary>
    private const int CriticalChance = 16;

    private readonly Random _random;
    private readonly SpellBook _spells;
    private readonly ItemBook _items;
    private readonly Party _party;
    private readonly Queue<string> _messages = new();
    private readonly Queue<PlannedAction> _turnQueue = new();
    private readonly List<PlannedAction> _plans = new();
    private readonly List<Combatant> _monsters;

    /// <summary>An order given, waiting for its place in the turn order. A null Actor marks a monster's turn, decided when it comes round.</summary>
    private record PlannedAction(Combatant Actor, BattleCommand Command, Spell? Spell, Item? Item, Combatant? Target);

    public Battle(Party party, IEnumerable<Combatant> monsters, SpellBook spells, ItemBook items, Random random)
    {
        _party = party;
        _monsters = monsters.ToList();
        _spells = spells;
        _items = items;
        _random = random;

        Say(_monsters.Count == 1
            ? $"{_monsters[0].Name} blocks the way!"
            : $"{_monsters.Count} foes block the way!");

        BeginPlanning();

        // Show the opening line straight away rather than waiting for the first
        // button press, which would otherwise open the fight on a blank panel.
        NextMessage();
    }

    public IReadOnlyList<Combatant> Party => _party.Members;
    public IReadOnlyList<Combatant> Monsters => _monsters;

    public BattlePhase Phase { get; private set; } = BattlePhase.Message;
    public BattleOutcome Outcome { get; private set; } = BattleOutcome.None;

    /// <summary>The line currently on show, or "" when not showing one.</summary>
    public string Message { get; private set; } = "";

    /// <summary>Who orders are being given for, or null when nobody is being planned.</summary>
    public Combatant? Planning { get; private set; }

    /// <summary>Commands available to whoever is being planned - Magic only appears for someone who can cast.</summary>
    public IReadOnlyList<BattleCommand> Commands { get; private set; } = Array.Empty<BattleCommand>();

    public int CommandIndex { get; private set; }
    public int SpellIndex { get; private set; }
    public int ItemIndex { get; private set; }
    public int TargetIndex { get; private set; }
    public int AllyIndex { get; private set; }

    public BattleCommand Command => Commands.Count == 0 ? BattleCommand.Fight : Commands[CommandIndex];

    /// <summary>Looks a spell up by key - so the screen can name and price every entry in a caster's list, not just the selected one.</summary>
    public Spell SpellFor(string key) => _spells[key];

    /// <summary>The spell under the cursor, or null when the one being planned knows none.</summary>
    public Spell? SelectedSpell =>
        Planning is { } who && who.Spells.Count > 0 ? _spells[who.Spells[SpellIndex]] : null;

    /// <summary>Looks an item up by key - so the screen can name and count every entry in the bag.</summary>
    public Item ItemFor(string key) => _items[key];

    /// <summary>The item under the cursor, or null when the bag is empty.</summary>
    public Item? SelectedItem =>
        Bag.Keys.Count > 0 ? _items[Bag.Keys[Math.Clamp(ItemIndex, 0, Bag.Keys.Count - 1)]] : null;

    /// <summary>The party's bag, so the screen can show counts and coin.</summary>
    public Inventory Bag => _party.Bag;

    public int ExperienceEarned { get; private set; }
    public int CoinEarned { get; private set; }

    // ------------------------------------------------------------ input ----

    /// <summary>Moves whichever cursor the current phase owns.</summary>
    public void MoveCursor(int delta)
    {
        if (delta == 0) return;
        var step = Math.Sign(delta);

        switch (Phase)
        {
            case BattlePhase.Command:
                CommandIndex = (CommandIndex + step + Commands.Count) % Commands.Count;
                break;

            case BattlePhase.Spell when Planning is { } caster && caster.Spells.Count > 0:
                SpellIndex = (SpellIndex + step + caster.Spells.Count) % caster.Spells.Count;
                break;

            case BattlePhase.Item when Bag.Keys.Count > 0:
                ItemIndex = (ItemIndex + step + Bag.Keys.Count) % Bag.Keys.Count;
                break;

            case BattlePhase.Target:
                TargetIndex = StepToLiving(_monsters, TargetIndex, step);
                break;

            case BattlePhase.AllyTarget when SelectedItem is { TargetsTheFallen: true }:
                AllyIndex = (AllyIndex + step + _party.Members.Count) % _party.Members.Count;
                break;

            case BattlePhase.AllyTarget:
                AllyIndex = StepToLiving(_party.Members, AllyIndex, step);
                break;
        }
    }

    /// <summary>Walks a cursor to the next one still standing - a cursor that can rest on the fallen invites a wasted turn.</summary>
    private static int StepToLiving(IReadOnlyList<Combatant> among, int from, int step)
    {
        for (var i = 0; i < among.Count; i++)
        {
            from = (from + step + among.Count) % among.Count;
            if (among[from].IsAlive) return from;
        }

        return from;
    }

    public void Cancel()
    {
        switch (Phase)
        {
            case BattlePhase.Spell:
            case BattlePhase.Item:
                Phase = BattlePhase.Command;
                break;

            case BattlePhase.Target:
            case BattlePhase.AllyTarget:
                // Back to whichever list this came from.
                Phase = Command switch
                {
                    BattleCommand.Magic => BattlePhase.Spell,
                    BattleCommand.Item => BattlePhase.Item,
                    _ => BattlePhase.Command
                };
                break;

            case BattlePhase.Command when _plans.Count > 0:
                // Take back the last order given and plan that character again.
                var previous = _plans[^1];
                _plans.RemoveAt(_plans.Count - 1);
                ReturnItem(previous);
                Planning = previous.Actor;
                Phase = BattlePhase.Command;
                RefreshCommands();
                break;
        }
    }

    /// <summary>The one button that drives everything: commits an order, picks a target, or turns the page.</summary>
    public void Confirm()
    {
        switch (Phase)
        {
            case BattlePhase.Message:
                NextMessage();
                break;

            case BattlePhase.Command:
                ConfirmCommand();
                break;

            case BattlePhase.Spell:
                ConfirmSpell();
                break;

            case BattlePhase.Item:
                ConfirmItem();
                break;

            case BattlePhase.Target:
                PlanAction(Command == BattleCommand.Magic ? SelectedSpell : null, null, _monsters[TargetIndex]);
                break;

            case BattlePhase.AllyTarget when Command == BattleCommand.Item:
                PlanAction(null, SelectedItem, _party.Members[AllyIndex]);
                break;

            case BattlePhase.AllyTarget:
                PlanAction(SelectedSpell, null, _party.Members[AllyIndex]);
                break;
        }
    }

    private void ConfirmCommand()
    {
        switch (Command)
        {
            case BattleCommand.Fight:
                TargetIndex = StepToLiving(_monsters, TargetIndex, _monsters[TargetIndex].IsAlive ? 0 : 1);
                if (!_monsters[TargetIndex].IsAlive) TargetIndex = StepToLiving(_monsters, TargetIndex, 1);
                Phase = BattlePhase.Target;
                break;

            case BattleCommand.Magic:
                SpellIndex = 0;
                Phase = BattlePhase.Spell;
                break;

            case BattleCommand.Item:
                ItemIndex = 0;
                Phase = BattlePhase.Item;
                break;

            case BattleCommand.Run:
                // Running is the whole party's decision, not one character's, so
                // it settles the round there and then.
                ResolveRun();
                break;
        }
    }

    private void ConfirmSpell()
    {
        var spell = SelectedSpell;
        if (spell is null || Planning is null) return;

        if (Planning.Mana < spell.Cost)
        {
            Say($"{Planning.Name} hasn't the strength for {spell.Name}.");
            NextMessage();
            return;
        }

        if (spell.TargetsEveryone)
        {
            PlanAction(spell, null, null);
            return;
        }

        if (spell.TargetsMonsters)
        {
            if (!_monsters[TargetIndex].IsAlive) TargetIndex = StepToLiving(_monsters, TargetIndex, 1);
            Phase = BattlePhase.Target;
        }
        else
        {
            if (!_party.Members[AllyIndex].IsAlive) AllyIndex = StepToLiving(_party.Members, AllyIndex, 1);
            Phase = BattlePhase.AllyTarget;
        }
    }

    // --------------------------------------------------------- planning ----

    private void BeginPlanning()
    {
        _plans.Clear();
        Planning = null;
        AdvancePlanning();
    }

    /// <summary>Moves on to the next character still standing who has no orders yet, or starts the round when everyone has some.</summary>
    private void AdvancePlanning()
    {
        var planned = _plans.Select(p => p.Actor).ToHashSet();
        var next = _party.Living.FirstOrDefault(m => !planned.Contains(m));

        if (next is null)
        {
            Planning = null;
            StartRound();
            return;
        }

        Planning = next;
        CommandIndex = 0;
        Phase = BattlePhase.Command;
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        var commands = new List<BattleCommand> { BattleCommand.Fight };
        if (Planning is { CanCast: true }) commands.Add(BattleCommand.Magic);
        if (Bag.Any) commands.Add(BattleCommand.Item);
        commands.Add(BattleCommand.Run);
        Commands = commands;
        CommandIndex = Math.Clamp(CommandIndex, 0, commands.Count - 1);
    }

    private void PlanAction(Spell? spell, Item? item, Combatant? target)
    {
        if (Planning is null) return;

        var command = item is not null ? BattleCommand.Item
            : spell is not null ? BattleCommand.Magic
            : BattleCommand.Fight;

        // The item leaves the bag now, when the order is given, not when the
        // turn runs - otherwise two characters could both be told to use the
        // last salve in the same round.
        if (item is not null && !Bag.Remove(item.Key)) return;

        _plans.Add(new PlannedAction(Planning, command, spell, item, target));
        AdvancePlanning();
    }

    /// <summary>An order taken back has to put its item back in the bag.</summary>
    private void ReturnItem(PlannedAction plan)
    {
        if (plan.Item is not null) Bag.Add(plan.Item.Key);
    }

    private void ConfirmItem()
    {
        var item = SelectedItem;
        if (item is null || Planning is null) return;

        if (!item.NeedsTarget)
        {
            PlanAction(null, item, null);
            return;
        }

        // A revive is the one thing pointed at somebody already down, so its
        // cursor starts on the first person who needs it.
        AllyIndex = item.TargetsTheFallen
            ? Math.Max(0, _party.Members.FindIndex(m => !m.IsAlive))
            : _party.Members.FindIndex(m => m.IsAlive);
        if (AllyIndex < 0) AllyIndex = 0;

        Phase = BattlePhase.AllyTarget;
    }

    // -------------------------------------------------------- resolution ----

    private void StartRound()
    {
        _turnQueue.Clear();

        // Party orders and monsters together, fastest first. Recomputed each
        // round rather than fixed, so a fight that fells the quick one changes
        // who goes next.
        var turns = new List<PlannedAction>(_plans);
        turns.AddRange(_monsters.Where(m => m.IsAlive)
            .Select(m => new PlannedAction(m, BattleCommand.Fight, null, null, null)));

        foreach (var turn in turns.OrderByDescending(t => t.Actor.Agility).ThenBy(_ => _random.Next()))
            _turnQueue.Enqueue(turn);

        NextMessage();
    }

    private void ResolveRun()
    {
        var fastestFoe = _monsters.Where(m => m.IsAlive).Select(m => m.Agility).DefaultIfEmpty(0).Max();
        var quickest = _party.Living.Select(m => m.Agility).DefaultIfEmpty(0).Max();
        var chance = Math.Clamp(0.55 + (quickest - fastestFoe) * 0.04, 0.15, 0.95);

        if (_random.NextDouble() < chance)
        {
            Say("The party breaks off and runs.");
            Outcome = BattleOutcome.Fled;
            NextMessage();
            return;
        }

        // A failed escape costs the round: everyone's turn is spent on it, and
        // the monsters still get theirs.
        Say("There's no getting away.");
        foreach (var plan in _plans) ReturnItem(plan);
        _plans.Clear();
        Planning = null;
        StartRound();
    }

    private void RunTurn(PlannedAction turn)
    {
        var actor = turn.Actor;

        if (_monsters.Contains(actor))
        {
            MonsterAttacks(actor);
            return;
        }

        if (turn.Item is { } item) UseItem(actor, item, turn.Target);
        else if (turn.Spell is { } spell) CastSpell(actor, spell, turn.Target);
        else PartyAttacks(actor, turn.Target);
    }

    private void PartyAttacks(Combatant actor, Combatant? intended)
    {
        var target = intended is { IsAlive: true } ? intended : _monsters.FirstOrDefault(m => m.IsAlive);
        if (target is null) return;

        Strike(actor, target, $"{actor.Name} strikes the {target.Name}");
        CheckMonstersDown(target);
    }

    private void CastSpell(Combatant caster, Spell spell, Combatant? intended)
    {
        if (caster.Mana < spell.Cost)
        {
            Say($"{caster.Name} hasn't the strength for {spell.Name}.");
            return;
        }

        caster.Mana -= spell.Cost;

        switch (spell.Kind)
        {
            case SpellKind.Damage:
            {
                var target = intended is { IsAlive: true } ? intended : _monsters.FirstOrDefault(m => m.IsAlive);
                if (target is null) return;
                var amount = Vary(spell.Power);
                target.Health = Math.Max(0, target.Health - amount);
                Say($"{caster.Name} {spell.Verb} the {target.Name}: {amount} damage.");
                CheckMonstersDown(target);
                break;
            }

            case SpellKind.DamageAll:
            {
                var struck = _monsters.Where(m => m.IsAlive).ToList();
                foreach (var monster in struck)
                    monster.Health = Math.Max(0, monster.Health - Vary(spell.Power));
                Say($"{caster.Name} {spell.Verb} them all.");
                foreach (var monster in struck.Where(m => !m.IsAlive))
                    Say($"The {monster.Name} goes down.");
                CheckVictory();
                break;
            }

            case SpellKind.Heal:
            {
                var ally = intended is { IsAlive: true } ? intended : caster;
                var amount = Restore(ally, Vary(spell.Power));
                Say($"{caster.Name} {spell.Verb} {ally.Name}: {amount} health.");
                break;
            }

            case SpellKind.HealAll:
            {
                foreach (var ally in _party.Living) Restore(ally, Vary(spell.Power));
                Say($"{caster.Name} {spell.Verb} the party.");
                break;
            }
        }
    }

    /// <summary>
    /// Spends an item that has already left the bag. Nothing here can fail for
    /// want of stock - that was settled when the order was given - so a wasted
    /// item is only ever wasted on a target that stopped needing it.
    /// </summary>
    private void UseItem(Combatant user, Item item, Combatant? intended)
    {
        switch (item.Kind)
        {
            case ItemKind.Heal:
            {
                var ally = intended is { IsAlive: true } ? intended : user;
                var amount = Restore(ally, Vary(item.Power));
                Say($"{user.Name} {item.Verb} {ally.Name}: {amount} health.");
                break;
            }

            case ItemKind.HealAll:
            {
                foreach (var ally in _party.Living) Restore(ally, Vary(item.Power));
                Say($"{user.Name} {item.Verb} the party.");
                break;
            }

            case ItemKind.Mana:
            {
                var ally = intended is { IsAlive: true } ? intended : user;
                var before = ally.Mana;
                ally.Mana = Math.Min(ally.MaxMana, ally.Mana + Vary(item.Power));
                Say($"{user.Name} {item.Verb} {ally.Name}: {ally.Mana - before} mana.");
                break;
            }

            case ItemKind.Revive:
            {
                var fallen = intended is { IsAlive: false } ? intended : _party.Members.FirstOrDefault(m => !m.IsAlive);
                if (fallen is null)
                {
                    Say($"{user.Name} finds nobody who needs it.");
                    break;
                }

                // Power is a percentage here, not a number of points.
                fallen.Health = Math.Max(1, fallen.MaxHealth * item.Power / 100);
                Say($"{user.Name} {item.Verb} {fallen.Name}, who comes round.");
                break;
            }
        }
    }

    private static int Restore(Combatant who, int amount)
    {
        var before = who.Health;
        who.Health = Math.Min(who.MaxHealth, who.Health + amount);
        return who.Health - before;
    }

    private void CheckMonstersDown(Combatant target)
    {
        if (!target.IsAlive) Say($"The {target.Name} goes down.");
        CheckVictory();
    }

    private void CheckVictory()
    {
        if (_monsters.Any(m => m.IsAlive)) return;

        ExperienceEarned = _monsters.Sum(m => m.Experience);
        CoinEarned = _monsters.Sum(m => m.Coin);
        _party.Bag.EarnCoin(CoinEarned);

        Say($"The way is clear. {ExperienceEarned} experience, {CoinEarned} coin.");
        foreach (var announcement in _party.AwardExperience(ExperienceEarned)) Say(announcement);
        Outcome = BattleOutcome.Victory;
    }

    private void MonsterAttacks(Combatant monster)
    {
        var standing = _party.Living.ToList();
        if (standing.Count == 0) return;

        var target = standing[_random.Next(standing.Count)];
        Strike(monster, target, $"The {monster.Name} lunges at {target.Name}");

        if (!target.IsAlive) Say($"{target.Name} goes down in the ash.");

        if (_party.Wiped)
        {
            Say("The party is finished.");
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

    /// <summary>+/-20% on a spell's power, so a known spell isn't a known number.</summary>
    private int Vary(int power)
    {
        var spread = Math.Max(1, power / 5);
        return Math.Max(1, power + _random.Next(-spread, spread + 1));
    }

    /// <summary>
    /// Attack power less half the target's defence, never below 1 - a fight
    /// always makes progress, however badly matched, so no encounter can
    /// deadlock with neither side able to hurt the other.
    /// </summary>
    public int DamageFrom(Combatant attacker, Combatant defender)
    {
        var baseDamage = Math.Max(1, attacker.Attack - defender.Defense / 2);
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
            var turn = _turnQueue.Dequeue();
            if (turn.Actor.IsAlive) RunTurn(turn); // felled earlier this round: their turn is gone
        }

        if (_messages.Count > 0)
        {
            Message = _messages.Dequeue();
            Phase = BattlePhase.Message;
            return;
        }

        Message = "";
        _turnQueue.Clear();

        if (Outcome != BattleOutcome.None)
        {
            Phase = BattlePhase.Over;
            return;
        }

        BeginPlanning();
    }
}
