using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattleCity.Core;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Presentation;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>What the HUD is waiting for; the input handlers branch on it.</summary>
public enum DuelUiMode
{
    Unbound,

    /// <summary>The opponent is acting; nothing is legal for the player.</summary>
    Waiting,

    /// <summary>The cursor roams the hand and the field; interact opens a card's menu.</summary>
    Free,

    /// <summary>A card's action menu is open.</summary>
    Menu,

    /// <summary>Choosing the target of an attack.</summary>
    Targets,

    /// <summary>Picking cards: an engine <c>Choice</c> or the tributes of a Summon.</summary>
    Picker,

    /// <summary>A response window: activate something or pass.</summary>
    Response,

    /// <summary>Browsing a Graveyard or Banished list.</summary>
    Pile,

    Ended,
}

/// <summary>
/// The duel HUD (systems.md §6.3, GDD §3.4): hand fan, phase bar, Life Point
/// counters, chain display, inspector, response prompt, <c>Choice</c> modals,
/// pile lists and the event log. Selection is a cursor over the hand and the
/// 3D anchors, moved by grid with the gamepad or keyboard and by hovering
/// with the mouse (cards are picked on the card layer). Every prompt is a
/// modal with explicit options; the HUD knows no card: menus come from
/// <see cref="ActionCatalog"/> and the legal actions of the engine.
/// </summary>
public partial class DuelUi : CanvasLayer
{
    /// <summary>Cursor rows from the far side: opponent's Spell &amp; Trap row, their monsters, the player's monsters, their Spell &amp; Traps, the hand.</summary>
    public const int OpponentSpellTrapRow = 0;
    public const int OpponentMonsterRow = 1;
    public const int MonsterRow = 2;
    public const int SpellTrapRow = 3;
    public const int HandRow = 4;
    public const int LogLines = 20;

    private const int ZoneCount = 5;
    private const float CardWidth = 156.0f;
    private const float CardHeight = CardWidth * 860.0f / 590.0f;
    private const float HandRaise = 44.0f;
    private const float NoticeTime = 2.5f;
    private const float HintTime = 6.0f;
    private static readonly Color _dim = new(1.0f, 1.0f, 1.0f, 0.45f);
    private static readonly Color _accent = new(1.0f, 0.93f, 0.55f);
    private static readonly Color _playerColor = new(0.55f, 0.9f, 1.0f);
    private static readonly Color _opponentColor = new(1.0f, 0.65f, 0.45f);

    private readonly HashSet<Guid> _highlighted = new();
    private readonly List<string> _log = new();
    private readonly List<TextureRect> _handCards = new();
    private readonly List<Guid> _handIds = new();
    private readonly HashSet<Guid> _picked = new();
    private readonly float[] _lpShown = { 8000.0f, 8000.0f };
    private readonly Tween?[] _lpTweens = new Tween?[2];
    private readonly HashSet<TutorialTopic> _hintsShown = new();
    private readonly Queue<TutorialTopic> _hintQueue = new();

    private DuelSession? _session;
    private DuelStaging? _staging;
    private int _player;
    private IReadOnlyList<PlayerCommand> _legal = Array.Empty<PlayerCommand>();
    private DuelUiMode _mode = DuelUiMode.Unbound;
    private int _row = HandRow;
    private int _col;
    private int _pickMin;
    private int _pickMax;
    private bool _pickerIsChoice;
    private Action<IReadOnlyCollection<Guid>>? _pickConfirm;
    private float _noticeTimer;
    private float _hintTimer;
    private Location _pileShown = Location.Graveyard;

    private Control _root = null!;
    private Panel _damageEdge = null!;
    private Tween? _damageTween;
    private Label _turnLabel = null!;
    private Label[] _phaseLabels = Array.Empty<Label>();
    private Button _advance = null!;
    private Label _lpOpponent = null!;
    private Label _lpPlayer = null!;
    private PanelContainer _chainPanel = null!;
    private HBoxContainer _chain = null!;
    private Label _banner = null!;
    private Label _notice = null!;
    private PanelContainer _hintPanel = null!;
    private Label _hintLabel = null!;
    private PanelContainer _inspector = null!;
    private Label _inspectorName = null!;
    private Label _inspectorType = null!;
    private Label _inspectorStats = null!;
    private Label _inspectorText = null!;
    private Control _hand = null!;
    private DuelListPanel _list = null!;
    private PanelContainer _logPanel = null!;
    private Label _logLabel = null!;
    private Label _hint = null!;

    public DuelUiMode Mode => _mode;

    /// <summary>Tutorial duel (GDD §6 step 2): each phase and each kind of prompt is explained the first time it appears.</summary>
    public bool Tutorial { get; set; }

    /// <summary>Hints shown since <see cref="Bind"/>.</summary>
    public int HintsShown => _hintsShown.Count;

    /// <summary>The hint on screen, empty when none.</summary>
    public string Hint => _hintPanel.Visible ? _hintLabel.Text : string.Empty;

    public int CursorRow => _row;

    /// <summary>Visual column, left to right from the player's camera (the opponent's zones read mirrored).</summary>
    public int CursorColumn => _col;

    public DuelListPanel List => _list;

    /// <summary>The cards ticked in the open picker.</summary>
    public IReadOnlySet<Guid> Picked => _picked;

    public IReadOnlySet<Guid> Highlighted => _highlighted;

    public IReadOnlyList<string> Log => _log;

    public int HandCount => _handCards.Count;

    public int ChainShown => _chain.GetChildCount();

    public string Banner => _banner.Text;

    public string Notice => _notice.Text;

    public bool LogVisible => _logPanel.Visible;

    /// <summary>Cards the mouse picked so far (hovers and clicks that moved the cursor).</summary>
    public int MousePicks { get; private set; }

    public DuelEngine? Engine => _session?.Engine;

    /// <summary>The screen centre of a fan card (mouse tests), or null.</summary>
    public Vector2? HandCardCenter(int index) =>
        index >= 0 && index < _handCards.Count ? _handCards[index].GlobalPosition + _handCards[index].Size / 2.0f : null;

    public override void _Ready()
    {
        Layer = 45;
        Build();
        _root.Visible = false;
    }

    /// <summary>Follows <paramref name="session"/> for the human seat and drives <paramref name="staging"/>'s highlights; the 3D hand row gives way to the fan.</summary>
    public void Bind(DuelSession session, DuelStaging staging)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(staging);
        Unbind();
        _session = session;
        _staging = staging;
        _player = session.HumanPlayer;
        staging.ShowPlayerHand = false;
        staging.Faces.FaceBaked += OnFaceBaked;
        session.Changed += Refresh;
        if (session.Engine is { } engine)
        {
            engine.EventRaised += OnEvent;
            foreach (DuelEvent e in engine.Events)
            {
                OnEvent(e);
            }

            _lpShown[0] = engine.State.Player(0).LifePoints;
            _lpShown[1] = engine.State.Player(1).LifePoints;
        }

        _row = HandRow;
        _col = 0;
        _hintsShown.Clear();
        _hintQueue.Clear();
        _hintTimer = 0.0f;
        _hintPanel.Visible = false;
        _root.Visible = true;
        Refresh();
    }

    public void Unbind()
    {
        if (_session is not null)
        {
            _session.Changed -= Refresh;
            if (_session.Engine is { } engine)
            {
                engine.EventRaised -= OnEvent;
            }
        }

        if (_staging is not null)
        {
            _staging.Faces.FaceBaked -= OnFaceBaked;
            _staging.ShowPlayerHand = true;
            foreach (Guid id in _highlighted)
            {
                if (_staging.Cards.TryGetValue(id, out CardView? view))
                {
                    view.SetSelected(false);
                }
            }
        }

        _highlighted.Clear();
        _session = null;
        _staging = null;
        _log.Clear();
        _list.Close();
        _hintQueue.Clear();
        _hintPanel.Visible = false;
        _mode = DuelUiMode.Unbound;
        _root.Visible = false;
    }

    /// <summary>The cursor cell of a card: field cards and the player's hand; null for piles, decks and the opponent's hand.</summary>
    public (int Row, int Column)? CellOf(Guid card)
    {
        if (Engine?.State.Find(card) is not { } instance)
        {
            return null;
        }

        return instance.Loc switch
        {
            Location.MonsterZone => (instance.Controller == _player ? MonsterRow : OpponentMonsterRow, VisualColumn(instance.Controller, instance.ZoneIndex)),
            Location.SpellTrapZone => (instance.Controller == _player ? SpellTrapRow : OpponentSpellTrapRow, VisualColumn(instance.Controller, instance.ZoneIndex)),
            Location.Hand when instance.Owner == _player => (HandRow, Engine.State.Player(_player).Hand.IndexOf(instance)),
            _ => null,
        };
    }

    /// <summary>The card under the cursor, or null on an empty zone.</summary>
    public CardInstance? CursorCard()
    {
        if (Engine is null)
        {
            return null;
        }

        DuelState s = Engine.State;
        PlayerState me = s.Player(_player);
        PlayerState them = s.Opponent(_player);
        return _row switch
        {
            OpponentSpellTrapRow => them.SpellTrapZones[ZoneIndex(them.Index, _col)],
            OpponentMonsterRow => them.MonsterZones[ZoneIndex(them.Index, _col)],
            MonsterRow => me.MonsterZones[ZoneIndex(me.Index, _col)],
            SpellTrapRow => me.SpellTrapZones[ZoneIndex(me.Index, _col)],
            _ => _col >= 0 && _col < me.Hand.Count ? me.Hand[_col] : null,
        };
    }

    /// <summary>Moves the cursor by grid (gamepad, keyboard) in <see cref="DuelUiMode.Free"/>, or the highlight of an open list.</summary>
    public void Navigate(int dx, int dy)
    {
        if (_mode is DuelUiMode.Menu or DuelUiMode.Targets or DuelUiMode.Picker or DuelUiMode.Response or DuelUiMode.Pile)
        {
            if (dy != 0)
            {
                _list.Move(dy);
            }

            return;
        }

        if (_mode is not DuelUiMode.Free || Engine is null)
        {
            return;
        }

        int handCount = Engine.State.Player(_player).Hand.Count;
        int row = _row;
        int col = _col;
        if (dy != 0)
        {
            int next = Math.Clamp(row + dy, 0, HandRow);
            if (next == HandRow && handCount == 0)
            {
                next = SpellTrapRow;
            }

            if (next != row)
            {
                col = row == HandRow ? HandToZoneColumn(col, handCount) : next == HandRow ? ZoneToHandColumn(col, handCount) : col;
                row = next;
            }
        }

        if (dx != 0)
        {
            int columns = row == HandRow ? Math.Max(handCount, 1) : ZoneCount;
            col = Math.Clamp(col + dx, 0, columns - 1);
        }

        SetCursor(row, col);
    }

    public void SetCursor(int row, int col)
    {
        _row = row;
        _col = col;
        RefreshCursorViews();
    }

    /// <summary>Interact: opens the menu of the cursor card, or presses the highlighted entry of an open list.</summary>
    public void Interact()
    {
        switch (_mode)
        {
            case DuelUiMode.Free:
                if (CursorCard() is { } card)
                {
                    OpenMenu(card);
                }

                break;
            case DuelUiMode.Menu:
            case DuelUiMode.Targets:
            case DuelUiMode.Picker:
            case DuelUiMode.Response:
                _list.Press();
                break;
            case DuelUiMode.Pile:
                ClosePile();
                break;
        }
    }

    /// <summary>Cancel: closes a menu, the target list, a tribute picker or a pile list. Engine prompts and response windows need an explicit answer.</summary>
    public void Cancel()
    {
        switch (_mode)
        {
            case DuelUiMode.Menu:
            case DuelUiMode.Targets:
                CloseList();
                break;
            case DuelUiMode.Picker when !_pickerIsChoice:
                CloseList();
                break;
            case DuelUiMode.Pile:
                ClosePile();
                break;
        }
    }

    /// <summary>The phase bar's advance: enters the Battle Phase when that is legal, else passes priority to move the phase on.</summary>
    public void AdvancePhase()
    {
        if (_mode != DuelUiMode.Free)
        {
            return;
        }

        if (ActionCatalog.Advance(_legal) is not { } command)
        {
            ShowNotice("Nothing to advance now");
            return;
        }

        Submit(command);
    }

    public void TogglePile(Location pile)
    {
        if (_mode == DuelUiMode.Pile)
        {
            bool same = _pileShown == pile;
            ClosePile();
            if (same)
            {
                return;
            }
        }

        if (_mode == DuelUiMode.Free)
        {
            OpenPile(pile);
        }
    }

    public void ToggleLog()
    {
        _logPanel.Visible = !_logPanel.Visible;
        UpdateLogLabel();
    }

    /// <summary>The Life Points a counter currently displays (they animate toward the state).</summary>
    public int LifePointsShown(int player) => Mathf.RoundToInt(_lpShown[player]);

    /// <summary>Screen-edge damage flashes shown so far (one per Life Point loss of the human seat; systems.md §6.4).</summary>
    public int DamageFlashes { get; private set; }

    public override void _Process(double delta)
    {
        if (_noticeTimer > 0.0f)
        {
            _noticeTimer -= (float)delta;
            _notice.Modulate = new Color(_accent, Mathf.Clamp(_noticeTimer / 0.5f, 0.0f, 1.0f));
            if (_noticeTimer <= 0.0f)
            {
                _notice.Text = string.Empty;
            }
        }

        if (_mode == DuelUiMode.Unbound)
        {
            return;
        }

        StepHints((float)delta);
        int dx = (Input.IsActionJustPressed(InputActions.MoveRight) ? 1 : 0) - (Input.IsActionJustPressed(InputActions.MoveLeft) ? 1 : 0);
        int dy = (Input.IsActionJustPressed(InputActions.MoveDown) ? 1 : 0) - (Input.IsActionJustPressed(InputActions.MoveUp) ? 1 : 0);
        if (dx != 0 || dy != 0)
        {
            Navigate(dx, dy);
        }

        if (Input.IsActionJustPressed(InputActions.Interact))
        {
            Interact();
        }
        else if (Input.IsActionJustPressed(InputActions.Cancel))
        {
            Cancel();
        }
        else if (Input.IsActionJustPressed(InputActions.DuelPhase))
        {
            AdvancePhase();
        }
        else if (Input.IsActionJustPressed(InputActions.DuelGraveyard))
        {
            TogglePile(Location.Graveyard);
        }
        else if (Input.IsActionJustPressed(InputActions.DuelBanished))
        {
            TogglePile(Location.Banished);
        }
        else if (Input.IsActionJustPressed(InputActions.DuelLog))
        {
            ToggleLog();
        }
    }

    /// <summary>Mouse over the 3D cards: hovering moves the cursor, a click acts; a click on a pile opens its list.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_mode != DuelUiMode.Free || _staging is null || GetViewport().GetCamera3D() is not { } camera)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion motion:
                if (PickCell(camera, motion.Position) is { } cell && (cell.Row != _row || cell.Column != _col))
                {
                    MousePicks++;
                    SetCursor(cell.Row, cell.Column);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click:
                {
                    CardView? view = _staging.Pick(camera, click.Position);
                    if (view?.Card is not { } card)
                    {
                        break;
                    }

                    if (CellOf(card.Id) is { } target)
                    {
                        MousePicks++;
                        SetCursor(target.Row, target.Column);
                        Interact();
                    }
                    else if (card.Loc is Location.Graveyard or Location.Banished)
                    {
                        OpenPile(card.Loc);
                    }

                    GetViewport().SetInputAsHandled();
                    break;
                }
        }
    }

    private (int Row, int Column)? PickCell(Camera3D camera, Vector2 screen) =>
        _staging?.Pick(camera, screen)?.Card is { } card ? CellOf(card.Id) : null;

    private int VisualColumn(int controller, int zone) => controller == _player ? zone : ZoneCount - 1 - zone;

    private int ZoneIndex(int controller, int visualColumn) => VisualColumn(controller, visualColumn);

    private static int HandToZoneColumn(int col, int handCount) => handCount <= 1 ? ZoneCount / 2 : (int)Math.Round(col * (ZoneCount - 1) / (double)(handCount - 1));

    private static int ZoneToHandColumn(int col, int handCount) => handCount <= 1 ? 0 : (int)Math.Round(col * (handCount - 1) / (double)(ZoneCount - 1));

    /// <summary>Re-reads the engine after every accepted command: closes what was open and decides what the player is asked next.</summary>
    private void Refresh()
    {
        _list.Close();
        _picked.Clear();
        _pickConfirm = null;
        if (Engine is null)
        {
            _mode = DuelUiMode.Unbound;
            return;
        }

        DuelState s = Engine.State;
        ClampCursor(s);
        UpdatePhaseBar(s);
        UpdateLifePoints(s);
        UpdateChain(s);
        UpdateHand(s);
        _legal = Engine.LegalActions(_player);
        if (!s.IsOver)
        {
            QueueHint(TutorialHints.Of(s.Phase));
        }

        if (s.IsOver)
        {
            _mode = DuelUiMode.Ended;
            _banner.Text = s.Winner == _player ? "Victory!" : s.Winner is null ? "Draw" : "Defeat";
            _advance.Disabled = true;
        }
        else if (_legal.Count == 0)
        {
            _mode = DuelUiMode.Waiting;
            _banner.Text = "Opponent's move…";
        }
        else if (s.PendingChoice is { } pending && pending.Player == _player)
        {
            _banner.Text = string.Empty;
            QueueHint(TutorialTopic.Picker);
            OpenChoice(pending);
        }
        else if (_legal.All(c => c is Discard))
        {
            _mode = DuelUiMode.Free;
            _banner.Text = Inv($"Discard down to {Engine.Options.HandLimit} cards");
            QueueHint(TutorialTopic.Discard);
        }
        else if (ActionCatalog.IsResponding(s, _player))
        {
            _banner.Text = string.Empty;
            QueueHint(TutorialTopic.Response);
            OpenResponse();
        }
        else
        {
            _mode = DuelUiMode.Free;
            _banner.Text = s.TurnPlayer == _player ? $"Your turn · {DuelText.PhaseName(s.Phase)} Phase" : "Opponent's turn";
        }

        _advance.Disabled = _mode != DuelUiMode.Free || ActionCatalog.Advance(_legal) is null;
        _advance.Text = ActionCatalog.AdvanceLabel(s, _legal);
        RefreshCursorViews();
    }

    private void ClampCursor(DuelState s)
    {
        int handCount = s.Player(_player).Hand.Count;
        if (_row == HandRow)
        {
            if (handCount == 0)
            {
                _row = SpellTrapRow;
                _col = Math.Clamp(_col, 0, ZoneCount - 1);
            }
            else
            {
                _col = Math.Clamp(_col, 0, handCount - 1);
            }
        }
        else
        {
            _col = Math.Clamp(_col, 0, ZoneCount - 1);
        }
    }

    private void RefreshCursorViews()
    {
        LayoutHand();
        UpdateHighlights();
        UpdateInspector();
    }

    private void Submit(PlayerCommand command)
    {
        if (_session is null)
        {
            return;
        }

        SubmitResult result = _session.Submit(command);
        if (!result.Accepted)
        {
            ShowNotice(result.Error ?? "Not allowed");
        }
    }

    private void OpenMenu(CardInstance card)
    {
        QueueHint(TutorialTopic.Menu);
        if (Engine is null)
        {
            return;
        }

        IReadOnlyList<CardAction> actions = ActionCatalog.ForCard(Engine, _legal, card.Id);
        if (actions.Count == 0)
        {
            ShowNotice(card.Controller == _player || card.Owner == _player ? $"Nothing to do with {card.Def.Name} now" : "Not your card");
            return;
        }

        var entries = actions.Select(a => new DuelListEntry(a.Label, () => ChooseAction(a), a, card.Id)).ToList();
        _mode = DuelUiMode.Menu;
        _list.Open(card.Def.Name, entries, "Cancel: back");
        RefreshCursorViews();
    }

    private void ChooseAction(CardAction action)
    {
        if (action.Single is { } single)
        {
            Submit(single);
        }
        else if (action.Kind == ActionKind.Attack)
        {
            OpenTargets(action);
        }
        else if (action.NeedsTributes)
        {
            OpenPicker(
                Inv($"Choose {action.TributesRequired} tribute{(action.TributesRequired > 1 ? "s" : string.Empty)}"),
                action.TributeOptions,
                action.TributesRequired,
                action.TributesRequired,
                isChoice: false,
                chosen =>
                {
                    if (action.WithTributes(chosen) is { } command)
                    {
                        Submit(command);
                    }
                    else
                    {
                        ShowNotice("Those monsters cannot be tributed together");
                    }
                });
        }
    }

    private void OpenTargets(CardAction attack)
    {
        QueueHint(TutorialTopic.Targets);
        if (Engine is null)
        {
            return;
        }

        DuelState s = Engine.State;
        var entries = new List<DuelListEntry>();
        foreach (Guid? target in attack.AttackTargets)
        {
            PlayerCommand? command = attack.Targeting(target);
            if (command is null)
            {
                continue;
            }

            string label = target is { } id ? TargetLabel(s, id) : "Direct attack";
            entries.Add(new DuelListEntry(label, () => Submit(command), command, target));
        }

        _mode = DuelUiMode.Targets;
        _list.Open("Attack target", entries, "Cancel: back");
        RefreshCursorViews();
    }

    private string TargetLabel(DuelState s, Guid id)
    {
        if (s.Find(id) is not { } card)
        {
            return "a card";
        }

        if (card.Controller != _player && card.IsFaceDown)
        {
            return "Face-down monster";
        }

        return card.IsInDefensePosition ? Inv($"{card.Def.Name} (DEF {card.DefValue})") : Inv($"{card.Def.Name} (ATK {card.Atk})");
    }

    private void OpenChoice(PendingChoice pending)
    {
        OpenPicker(pending.Choice.Prompt, pending.Choice.Options, pending.Choice.Min, pending.Choice.Max, isChoice: true, chosen => Submit(new AnswerChoice(_player, chosen.ToList())));
    }

    private void OpenPicker(string prompt, IReadOnlyList<Guid> options, int min, int max, bool isChoice, Action<IReadOnlyCollection<Guid>> confirm)
    {
        if (Engine is null)
        {
            return;
        }

        DuelState s = Engine.State;
        _picked.Clear();
        _pickMin = min;
        _pickMax = Math.Min(max, options.Count);
        _pickerIsChoice = isChoice;
        _pickConfirm = confirm;
        var entries = new List<DuelListEntry>();
        for (int i = 0; i < options.Count; i++)
        {
            Guid id = options[i];
            int index = i;
            entries.Add(new DuelListEntry(PickLabel(s, id, false), () => TogglePick(index, id), id, id));
        }

        entries.Add(new DuelListEntry(_pickMin == _pickMax ? "Confirm" : Inv($"Confirm ({_pickMin}–{_pickMax})"), ConfirmPick, "confirm"));
        if (min == 0)
        {
            entries.Add(new DuelListEntry("Decline", () => _pickConfirm?.Invoke(Array.Empty<Guid>()), "decline"));
        }

        _mode = DuelUiMode.Picker;
        _list.Open(prompt, entries, isChoice ? null : "Cancel: back");
        RefreshCursorViews();
    }

    private string PickLabel(DuelState s, Guid id, bool picked)
    {
        string mark = picked ? "[x] " : "[ ] ";
        if (s.Find(id) is not { } card)
        {
            return mark + "Option";
        }

        string where = card.Loc switch
        {
            Location.Hand => "hand",
            Location.MonsterZone or Location.SpellTrapZone or Location.FieldZone => card.Controller == _player ? "your field" : "opponent's field",
            Location.Graveyard => card.Owner == _player ? "your Graveyard" : "opponent's Graveyard",
            Location.Banished => "banished",
            Location.Deck => "Deck",
            Location.FusionDeck => "Fusion Deck",
            _ => card.Loc.ToString(),
        };
        string name = card.Controller != _player && card.IsOnField && card.IsFaceDown ? "Face-down card" : card.Def.Name;
        return $"{mark}{name} ({where})";
    }

    private void TogglePick(int index, Guid id)
    {
        if (Engine is null)
        {
            return;
        }

        if (!_picked.Remove(id))
        {
            if (_picked.Count >= _pickMax && _pickMax == 1)
            {
                Guid previous = _picked.First();
                _picked.Clear();
                int previousIndex = _list.Entries.ToList().FindIndex(e => e.Card == previous);
                if (previousIndex >= 0)
                {
                    _list.SetLabel(previousIndex, PickLabel(Engine.State, previous, false));
                }
            }
            else if (_picked.Count >= _pickMax)
            {
                ShowNotice(Inv($"At most {_pickMax}"));
                return;
            }

            _picked.Add(id);
        }

        _list.SetLabel(index, PickLabel(Engine.State, id, _picked.Contains(id)));
    }

    private void ConfirmPick()
    {
        if (_picked.Count < _pickMin || _picked.Count > _pickMax)
        {
            ShowNotice(_pickMin == _pickMax ? Inv($"Choose exactly {_pickMin}") : Inv($"Choose {_pickMin} to {_pickMax}"));
            return;
        }

        _pickConfirm?.Invoke(_picked.ToList());
    }

    private void OpenResponse()
    {
        if (Engine is null)
        {
            return;
        }

        DuelState s = Engine.State;
        var entries = new List<DuelListEntry>();
        foreach (PlayerCommand command in ActionCatalog.Responses(_legal))
        {
            PlayerCommand c = command;
            entries.Add(new DuelListEntry(DuelText.Describe(c, s, _player), () => Submit(c), c, ActionCatalog.CardOf(c)));
        }

        if (_legal.FirstOrDefault(c => c is Pass) is { } pass)
        {
            entries.Add(new DuelListEntry("Pass", () => Submit(pass), pass));
        }

        _mode = DuelUiMode.Response;
        _list.Open(DuelText.ResponseText(s, _player), entries);
        RefreshCursorViews();
    }

    private void OpenPile(Location pile)
    {
        if (Engine is null)
        {
            return;
        }

        DuelState s = Engine.State;
        _pileShown = pile;
        var entries = new List<DuelListEntry>();
        string name = pile == Location.Graveyard ? "Graveyard" : "Banished";
        foreach ((string owner, PlayerState p) in new[] { ("Your", s.Player(_player)), ("Opponent's", s.Opponent(_player)) })
        {
            List<CardInstance> cards = pile == Location.Graveyard ? p.Graveyard : p.Banished;
            entries.Add(new DuelListEntry(Inv($"— {owner} {name} ({cards.Count}) —"), null, enabled: false));
            foreach (CardInstance card in Enumerable.Reverse(cards))
            {
                entries.Add(new DuelListEntry(card.Def.Name, null, card.Id, card.Id));
            }
        }

        if (entries.All(e => !e.Enabled))
        {
            entries.Add(new DuelListEntry("(empty)", null, enabled: false));
            entries.Add(new DuelListEntry("Close", ClosePile, "close"));
        }

        _mode = DuelUiMode.Pile;
        _list.Open(name, entries, "Cancel: close");
        RefreshCursorViews();
    }

    private void ClosePile()
    {
        _list.Close();
        _mode = DuelUiMode.Free;
        RefreshCursorViews();
    }

    private void CloseList()
    {
        _list.Close();
        _picked.Clear();
        _pickConfirm = null;
        _mode = DuelUiMode.Free;
        RefreshCursorViews();
    }

    private void ShowNotice(string text)
    {
        _notice.Text = text;
        _notice.Modulate = _accent;
        _noticeTimer = NoticeTime;
    }

    /// <summary>Queues a tutorial hint the first time its topic comes up; nothing outside the tutorial duel.</summary>
    private void QueueHint(TutorialTopic topic)
    {
        if (!Tutorial || !_hintsShown.Add(topic))
        {
            return;
        }

        _hintQueue.Enqueue(topic);
        if (!_hintPanel.Visible)
        {
            StepHints(0.0f);
        }
    }

    /// <summary>Shows queued hints one at a time for <see cref="HintTime"/> each.</summary>
    private void StepHints(float dt)
    {
        if (_hintPanel.Visible)
        {
            _hintTimer -= dt;
            if (_hintTimer > 0.0f)
            {
                return;
            }

            _hintPanel.Visible = false;
        }

        if (_hintQueue.Count == 0)
        {
            return;
        }

        _hintLabel.Text = TutorialHints.Text(_hintQueue.Dequeue());
        _hintPanel.Visible = true;
        _hintTimer = HintTime;
    }

    private void OnEvent(DuelEvent e)
    {
        if (Engine is null || DuelText.Describe(e, Engine.State, _player) is not { } text)
        {
            return;
        }

        _log.Add(text);
        if (_log.Count > LogLines)
        {
            _log.RemoveAt(0);
        }

        if (_logPanel.Visible)
        {
            UpdateLogLabel();
        }
    }

    private void OnFaceBaked(string cardId, Texture2D texture)
    {
        foreach (TextureRect rect in _handCards)
        {
            if (rect.GetMeta("card_id", string.Empty).AsString() == cardId)
            {
                rect.Texture = texture;
            }
        }
    }

    private void UpdatePhaseBar(DuelState s)
    {
        _turnLabel.Text = Inv($"Turn {s.TurnNumber} · {(s.TurnPlayer == _player ? "You" : "Opponent")}");
        _turnLabel.Modulate = s.TurnPlayer == _player ? _playerColor : _opponentColor;
        for (int i = 0; i < _phaseLabels.Length; i++)
        {
            bool current = (int)s.Phase == i;
            _phaseLabels[i].Modulate = current ? _accent : _dim;
        }
    }

    private void UpdateLifePoints(DuelState s)
    {
        for (int i = 0; i < 2; i++)
        {
            int target = s.Player(i).LifePoints;
            if (Mathf.RoundToInt(_lpShown[i]) == target)
            {
                continue;
            }

            if (i == DuelDirector.HumanSeat && target < Mathf.RoundToInt(_lpShown[i]) && _lpShown[i] > 0.0f)
            {
                FlashDamageEdge();
            }

            int index = i;
            _lpTweens[i]?.Kill();
            _lpTweens[i] = CreateTween();
            _lpTweens[i]!.TweenMethod(Callable.From<float>(v =>
            {
                _lpShown[index] = v;
                SetLifePointLabel(index);
            }), _lpShown[i], (float)target, 0.6f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }

        SetLifePointLabel(0);
        SetLifePointLabel(1);
    }

    /// <summary>The screen-edge damage overlay (systems.md §6.4 HitPulse companion): a red vignette that fades over half a second.</summary>
    private void FlashDamageEdge()
    {
        DamageFlashes++;
        _damageTween?.Kill();
        _damageEdge.Visible = true;
        _damageEdge.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.85f);
        _damageTween = CreateTween();
        _damageTween.TweenProperty(_damageEdge, "modulate:a", 0.0f, 0.5f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _damageTween.TweenCallback(Callable.From(() => _damageEdge.Visible = false));
    }

    private void SetLifePointLabel(int player)
    {
        Label label = player == _player ? _lpPlayer : _lpOpponent;
        label.Text = Inv($"{(player == _player ? "You" : "Opponent")}  {Mathf.RoundToInt(_lpShown[player])}");
    }

    private void UpdateChain(DuelState s)
    {
        foreach (Node child in _chain.GetChildren())
        {
            _chain.RemoveChild(child);
            child.QueueFree();
        }

        foreach (ChainLink link in s.Chain)
        {
            var label = new Label { Text = Inv($"{link.Index + 1}. {link.Source.Def.Name}") };
            label.AddThemeFontSizeOverride("font_size", 18);
            label.Modulate = link.Player == _player ? _playerColor : _opponentColor;
            _chain.AddChild(label);
        }

        _chainPanel.Visible = s.Chain.Count > 0;
    }

    private void UpdateHand(DuelState s)
    {
        List<CardInstance> hand = s.Player(_player).Hand;
        if (hand.Count == _handIds.Count && hand.Select(c => c.Id).SequenceEqual(_handIds))
        {
            return;
        }

        foreach (TextureRect rect in _handCards)
        {
            rect.QueueFree();
        }

        _handCards.Clear();
        _handIds.Clear();
        for (int i = 0; i < hand.Count; i++)
        {
            CardInstance card = hand[i];
            int index = i;
            var rect = new TextureRect
            {
                Name = $"Hand{i}",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new Vector2(CardWidth, CardHeight),
                PivotOffset = new Vector2(CardWidth / 2.0f, CardHeight),
                MouseFilter = Control.MouseFilterEnum.Stop,
                Texture = _staging?.Faces.Get(card.Def),
            };
            rect.SetMeta("card_id", card.Def.Id);
            rect.MouseEntered += () =>
            {
                if (_mode == DuelUiMode.Free && (_row != HandRow || _col != index))
                {
                    MousePicks++;
                    SetCursor(HandRow, index);
                }
            };
            rect.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && _mode == DuelUiMode.Free)
                {
                    SetCursor(HandRow, index);
                    Interact();
                    _hand.AcceptEvent();
                }
            };
            _hand.AddChild(rect);
            _handCards.Add(rect);
            _handIds.Add(card.Id);
        }

        LayoutHand();
    }

    private void LayoutHand()
    {
        int n = _handCards.Count;
        if (n == 0)
        {
            return;
        }

        float width = _root.Size.X;
        float spacing = Math.Min(CardWidth * 0.78f, 1100.0f / n);
        float total = spacing * (n - 1) + CardWidth;
        float x0 = (width - total) / 2.0f;
        float baseY = _hand.Size.Y - CardHeight - 24.0f;
        for (int i = 0; i < n; i++)
        {
            bool selected = _row == HandRow && _col == i;
            float lift = selected ? HandRaise : 0.0f;
            float angle = (i - (n - 1) / 2.0f) * 2.5f;
            _handCards[i].Position = new Vector2(x0 + i * spacing, baseY - lift + Math.Abs(i - (n - 1) / 2.0f) * 6.0f);
            _handCards[i].Rotation = Mathf.DegToRad(selected ? 0.0f : angle);
            _handCards[i].Modulate = selected ? Colors.White : new Color(0.85f, 0.85f, 0.85f);
            _handCards[i].ZIndex = selected ? 10 : i;
        }
    }

    private void UpdateHighlights()
    {
        var wanted = new HashSet<Guid>();
        if (_mode == DuelUiMode.Free && CursorCard() is { } cursor)
        {
            wanted.Add(cursor.Id);
        }

        if (_list.IsOpen && _list.Current?.Card is { } listed)
        {
            wanted.Add(listed);
        }

        if (_mode == DuelUiMode.Picker)
        {
            wanted.UnionWith(_picked);
        }

        if (_staging is not null)
        {
            foreach (Guid id in _highlighted.Where(id => !wanted.Contains(id)))
            {
                if (_staging.Cards.TryGetValue(id, out CardView? view))
                {
                    view.SetSelected(false);
                }
            }

            foreach (Guid id in wanted.Where(id => !_highlighted.Contains(id)))
            {
                if (_staging.Cards.TryGetValue(id, out CardView? view))
                {
                    view.SetSelected(true);
                }
            }
        }

        _highlighted.Clear();
        _highlighted.UnionWith(wanted);
    }

    private void UpdateInspector()
    {
        CardInstance? card = null;
        if (_list.IsOpen && _list.Current?.Card is { } listed)
        {
            card = Engine?.State.Find(listed);
        }

        card ??= _mode is DuelUiMode.Free or DuelUiMode.Menu or DuelUiMode.Targets ? CursorCard() : null;
        if (card is null)
        {
            _inspector.Visible = false;
            return;
        }

        _inspector.Visible = true;
        bool hidden = card.Controller != _player && (card.Loc == Location.Hand || (card.IsOnField && card.IsFaceDown));
        if (hidden)
        {
            _inspectorName.Text = "Face-down card";
            _inspectorType.Text = card.Loc == Location.Hand ? "In the opponent's hand" : DuelText.PositionName(card.Pos);
            _inspectorStats.Text = string.Empty;
            _inspectorText.Text = string.Empty;
            return;
        }

        _inspectorName.Text = card.Def.Name;
        _inspectorType.Text = DuelText.TypeLine(card.Def) + (card.IsOnField ? " · " + DuelText.PositionName(card.Pos) : string.Empty);
        _inspectorStats.Text = DuelText.StatLine(card);
        _inspectorText.Text = card.Def.Text;
    }

    private void UpdateLogLabel()
    {
        _logLabel.Text = _log.Count == 0 ? "(nothing yet)" : string.Join("\n", _log);
    }

    private void Build()
    {
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.Resized += LayoutHand;
        AddChild(_root);

        // Damage overlay: a thick translucent red border, hidden until the human seat loses Life Points.
        var edge = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color(0.9f, 0.15f, 0.1f, 0.55f) };
        edge.SetBorderWidthAll(56);
        edge.SetCornerRadiusAll(0);
        _damageEdge = new Panel { Name = "DamageEdge", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _damageEdge.AddThemeStyleboxOverride("panel", edge);
        _damageEdge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_damageEdge);

        // Phase bar, top-left (GDD §3.4).
        PanelContainer phasePanel = Panel("PhaseBar");
        Place(phasePanel, 0.0f, 0.0f, 24.0f, 20.0f, 0.0f, 0.0f, Control.GrowDirection.End, Control.GrowDirection.End);
        _root.AddChild(phasePanel);
        var phaseRow = new HBoxContainer();
        phaseRow.AddThemeConstantOverride("separation", 14);
        phasePanel.AddChild(phaseRow);
        _turnLabel = Text(phaseRow, "Turn 1", 20);
        _phaseLabels = new Label[6];
        for (int i = 0; i < 6; i++)
        {
            _phaseLabels[i] = Text(phaseRow, DuelText.PhaseName((Phase)i), 18);
        }

        _advance = new Button { Text = "End turn", FocusMode = Control.FocusModeEnum.None };
        _advance.AddThemeFontSizeOverride("font_size", 18);
        _advance.Pressed += AdvancePhase;
        phaseRow.AddChild(_advance);

        // Life Points, top-right.
        PanelContainer lpPanel = Panel("LifePoints");
        Place(lpPanel, 1.0f, 0.0f, -24.0f - 260.0f, 20.0f, 260.0f, 0.0f, Control.GrowDirection.Begin, Control.GrowDirection.End);
        _root.AddChild(lpPanel);
        var lpColumn = new VBoxContainer();
        lpPanel.AddChild(lpColumn);
        _lpOpponent = Text(lpColumn, "Opponent  8000", 26);
        _lpOpponent.HorizontalAlignment = HorizontalAlignment.Right;
        _lpOpponent.Modulate = _opponentColor;
        _lpPlayer = Text(lpColumn, "You  8000", 26);
        _lpPlayer.HorizontalAlignment = HorizontalAlignment.Right;
        _lpPlayer.Modulate = _playerColor;

        // Chain display, top-centre.
        _chainPanel = Panel("Chain");
        Place(_chainPanel, 0.5f, 0.0f, 0.0f, 20.0f, 0.0f, 0.0f, Control.GrowDirection.Both, Control.GrowDirection.End);
        _chainPanel.Visible = false;
        _root.AddChild(_chainPanel);
        _chain = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _chain.AddThemeConstantOverride("separation", 18);
        _chainPanel.AddChild(_chain);

        _banner = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _banner.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _banner.OffsetTop = 76.0f;
        _banner.OffsetBottom = 112.0f;
        _banner.AddThemeFontSizeOverride("font_size", 26);
        _root.AddChild(_banner);
        _notice = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _notice.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _notice.OffsetTop = 116.0f;
        _notice.OffsetBottom = 146.0f;
        _notice.AddThemeFontSizeOverride("font_size", 20);
        _root.AddChild(_notice);

        // Tutorial hint, under the notice.
        _hintPanel = Panel("Hint");
        Place(_hintPanel, 0.5f, 0.0f, -360.0f, 152.0f, 720.0f, 0.0f, Control.GrowDirection.Both, Control.GrowDirection.End);
        _hintPanel.Visible = false;
        _hintPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(_hintPanel);
        _hintLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _hintLabel.CustomMinimumSize = new Vector2(680.0f, 0.0f);
        _hintLabel.AddThemeFontSizeOverride("font_size", 18);
        _hintLabel.Modulate = _accent;
        _hintPanel.AddChild(_hintLabel);

        // Inspector, right.
        _inspector = Panel("Inspector");
        Place(_inspector, 1.0f, 0.0f, -24.0f - 380.0f, 130.0f, 380.0f, 0.0f, Control.GrowDirection.Begin, Control.GrowDirection.End);
        _inspector.Visible = false;
        _root.AddChild(_inspector);
        var inspectorColumn = new VBoxContainer();
        inspectorColumn.AddThemeConstantOverride("separation", 6);
        _inspector.AddChild(inspectorColumn);
        _inspectorName = Text(inspectorColumn, string.Empty, 24);
        _inspectorName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inspectorType = Text(inspectorColumn, string.Empty, 16);
        _inspectorType.Modulate = _dim;
        _inspectorType.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inspectorStats = Text(inspectorColumn, string.Empty, 20);
        _inspectorText = Text(inspectorColumn, string.Empty, 17);
        _inspectorText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _inspectorText.CustomMinimumSize = new Vector2(340.0f, 0.0f);

        // Event log, left (toggle).
        _logPanel = Panel("Log");
        Place(_logPanel, 0.0f, 0.0f, 24.0f, 130.0f, 440.0f, 0.0f, Control.GrowDirection.End, Control.GrowDirection.End);
        _logPanel.Visible = false;
        _root.AddChild(_logPanel);
        _logLabel = Text(_logPanel, "(nothing yet)", 15);
        _logLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _logLabel.CustomMinimumSize = new Vector2(410.0f, 0.0f);

        // Pile buttons and the control hint, bottom-left and bottom-right.
        var buttons = new HBoxContainer();
        Place(buttons, 0.0f, 1.0f, 24.0f, -24.0f - 40.0f, 0.0f, 40.0f, Control.GrowDirection.End, Control.GrowDirection.Begin);
        buttons.AddThemeConstantOverride("separation", 8);
        _root.AddChild(buttons);
        foreach ((string text, Action action) in new (string, Action)[]
        {
            ("Graveyard", () => TogglePile(Location.Graveyard)),
            ("Banished", () => TogglePile(Location.Banished)),
            ("Log", ToggleLog),
        })
        {
            var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Pressed += action;
            buttons.AddChild(button);
        }

        _hint = new Label
        {
            Text = "A / E: act   B / Esc: back   Y / Space: phase   LB / G: graveyard   RB / B: banished   Back / L: log",
            HorizontalAlignment = HorizontalAlignment.Right,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Place(_hint, 1.0f, 1.0f, -24.0f - 1100.0f, -24.0f - 24.0f, 1100.0f, 24.0f, Control.GrowDirection.Begin, Control.GrowDirection.Begin);
        _hint.AddThemeFontSizeOverride("font_size", 14);
        _hint.Modulate = _dim;
        _root.AddChild(_hint);

        // Hand fan, bottom-centre.
        _hand = new Control { Name = "Hand", MouseFilter = Control.MouseFilterEnum.Ignore };
        _hand.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _hand.OffsetTop = -260.0f;
        _hand.OffsetBottom = 0.0f;
        _hand.OffsetLeft = 0.0f;
        _hand.OffsetRight = 0.0f;
        _hand.GrowVertical = Control.GrowDirection.Begin;
        _root.AddChild(_hand);

        // The one list panel: menus, targets, prompts, pickers and piles. Centred a little below the middle.
        _list = new DuelListPanel { Name = "List" };
        _list.AddThemeStyleboxOverride("panel", PanelStyle());
        Place(_list, 0.5f, 0.55f, 0.0f, 0.0f, 0.0f, 0.0f, Control.GrowDirection.Both, Control.GrowDirection.Both);
        _list.Highlighted += _ => RefreshCursorViews();
        _root.AddChild(_list);
    }

    /// <summary>Pins a control to an anchor point of the root with an offset and a minimum size; growth directions say where extra size goes.</summary>
    private static void Place(Control control, float anchorX, float anchorY, float offsetX, float offsetY, float minWidth, float minHeight, Control.GrowDirection growHorizontal, Control.GrowDirection growVertical)
    {
        control.AnchorLeft = anchorX;
        control.AnchorRight = anchorX;
        control.AnchorTop = anchorY;
        control.AnchorBottom = anchorY;
        control.OffsetLeft = offsetX;
        control.OffsetRight = offsetX + minWidth;
        control.OffsetTop = offsetY;
        control.OffsetBottom = offsetY + minHeight;
        control.CustomMinimumSize = new Vector2(minWidth, minHeight);
        control.GrowHorizontal = growHorizontal;
        control.GrowVertical = growVertical;
    }

    private static PanelContainer Panel(string name)
    {
        var panel = new PanelContainer { Name = name };
        panel.AddThemeStyleboxOverride("panel", PanelStyle());
        return panel;
    }

    private static StyleBoxFlat PanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.11f, 0.86f),
            BorderColor = new Color(0.45f, 0.8f, 1.0f, 0.6f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(12.0f);
        return style;
    }

    private static Label Text(Node parent, string text, int size)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(label);
        return label;
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
