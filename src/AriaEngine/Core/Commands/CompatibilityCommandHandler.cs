namespace AriaEngine.Core.Commands;

public sealed class CompatibilityCommandHandler : BaseCommandHandler
{
    public override IReadOnlySet<OpCode> HandledCodes { get; } = new HashSet<OpCode>
    {
        OpCode.ChapterSelect,
        OpCode.UnlockChapter,
        OpCode.ChapterThumbnail,
        OpCode.ChapterCard,
        OpCode.ChapterScroll,
        OpCode.ChapterProgress,
        OpCode.CharLoad,
        OpCode.CharShow,
        OpCode.CharHide,
        OpCode.CharMove,
        OpCode.CharExpression,
        OpCode.CharPose,
        OpCode.CharZ,
        OpCode.CharScale,
        OpCode.ChangeScene,
        OpCode.ReturnScene,
        OpCode.SetSceneData,
        OpCode.GetSceneData,
        OpCode.DefChapter,
        OpCode.ChapterId,
        OpCode.ChapterTitle,
        OpCode.ChapterDesc,
        OpCode.ChapterScript,
        OpCode.EndChapter
    };

    public CompatibilityCommandHandler(VirtualMachine vm) : base(vm)
    {
    }

    public override bool Execute(Instruction inst)
    {
        switch (inst.Op)
        {
            case OpCode.ChapterSelect:
                ExecuteChapterSelect();
                return true;

            case OpCode.UnlockChapter:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.ChapterManager.UnlockChapter(GetVal(inst.Arguments[0]));
                Vm.ChapterManager.SaveChapters();
                return true;

            case OpCode.ChapterThumbnail:
                if (!ValidateArgs(inst, 2)) return true;
                {
                    var chapter = Vm.ChapterManager.GetChapter(GetVal(inst.Arguments[0]));
                    if (chapter != null)
                    {
                        chapter.ThumbnailPath = GetString(inst.Arguments[1]);
                        Vm.ChapterManager.SaveChapters();
                    }
                }
                return true;

            case OpCode.ChapterCard:
                if (!ValidateArgs(inst, 5)) return true;
                ExecuteChapterCard(inst);
                return true;

            case OpCode.ChapterScroll:
                return true;

            case OpCode.ChapterProgress:
                if (!ValidateArgs(inst, 2)) return true;
                Vm.ChapterManager.UpdateProgress(GetVal(inst.Arguments[0]), GetVal(inst.Arguments[1]));
                Vm.ChapterManager.SaveChapters();
                return true;

            case OpCode.CharLoad:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.CharacterManager.LoadCharacterData(GetString(inst.Arguments[0]));
                return true;

            case OpCode.CharShow:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.CharacterManager.ShowCharacter(
                    inst.Arguments[0],
                    inst.Arguments.Count > 1 ? inst.Arguments[1] : "normal",
                    inst.Arguments.Count > 2 ? inst.Arguments[2] : "default");
                return true;

            case OpCode.CharHide:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.CharacterManager.HideCharacter(inst.Arguments[0], inst.Arguments.Count > 1 ? GetVal(inst.Arguments[1]) : 300);
                return true;

            case OpCode.CharMove:
                if (!ValidateArgs(inst, 3)) return true;
                Vm.CharacterManager.MoveCharacter(
                    inst.Arguments[0],
                    GetVal(inst.Arguments[1]),
                    GetVal(inst.Arguments[2]),
                    inst.Arguments.Count > 3 ? GetVal(inst.Arguments[3]) : 500);
                return true;

            case OpCode.CharExpression:
                if (!ValidateArgs(inst, 2)) return true;
                Vm.CharacterManager.ChangeExpression(inst.Arguments[0], inst.Arguments[1]);
                return true;

            case OpCode.CharPose:
                if (!ValidateArgs(inst, 2)) return true;
                Vm.CharacterManager.ChangePose(inst.Arguments[0], inst.Arguments[1]);
                return true;

            case OpCode.CharZ:
                if (!ValidateArgs(inst, 2)) return true;
                Vm.CharacterManager.SetCharacterZ(inst.Arguments[0], GetVal(inst.Arguments[1]));
                return true;

            case OpCode.CharScale:
                if (!ValidateArgs(inst, 2)) return true;
                Vm.CharacterManager.SetCharacterScale(inst.Arguments[0], GetFloat(inst.Arguments[1], inst, 1.0f));
                return true;

            case OpCode.ChangeScene:
                if (!ValidateArgs(inst, 1)) return true;
                Vm.GameFlow.TransitionTo(ParseScene(inst.Arguments[0]), Vm);
                return true;

            case OpCode.ReturnScene:
                Vm.GameFlow.GoBack(Vm);
                return true;

            case OpCode.SetSceneData:
                if (!ValidateArgs(inst, 2)) return true;
                State.SceneRuntime.SceneData[inst.Arguments[0]] = GetString(inst.Arguments[1]);
                return true;

            case OpCode.GetSceneData:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.SceneRuntime.SceneData.TryGetValue(inst.Arguments[0], out object? value) && value != null)
                {
                    string str = value.ToString() ?? "";
                    SetReg(inst.Arguments[0], int.TryParse(str, out int num) ? num : 0);
                }
                else
                {
                    SetReg(inst.Arguments[0], 0);
                }
                return true;

            case OpCode.DefChapter:
                State.FlagRuntime.CurrentChapterDefinition = new ChapterInfo();
                return true;

            case OpCode.ChapterId:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.FlagRuntime.CurrentChapterDefinition != null) State.FlagRuntime.CurrentChapterDefinition.Id = GetVal(inst.Arguments[0]);
                return true;

            case OpCode.ChapterTitle:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.FlagRuntime.CurrentChapterDefinition != null) State.FlagRuntime.CurrentChapterDefinition.Title = GetString(inst.Arguments[0]);
                return true;

            case OpCode.ChapterDesc:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.FlagRuntime.CurrentChapterDefinition != null) State.FlagRuntime.CurrentChapterDefinition.Description = GetString(inst.Arguments[0]);
                return true;

            case OpCode.ChapterScript:
                if (!ValidateArgs(inst, 1)) return true;
                if (State.FlagRuntime.CurrentChapterDefinition != null) State.FlagRuntime.CurrentChapterDefinition.ScriptPath = GetString(inst.Arguments[0]);
                return true;

            case OpCode.EndChapter:
                if (State.FlagRuntime.CurrentChapterDefinition != null)
                {
                    Vm.ChapterManager.AddChapter(State.FlagRuntime.CurrentChapterDefinition);
                    State.FlagRuntime.CurrentChapterDefinition = null;
                }
                return true;

            default:
                return false;
        }
    }

    private void ExecuteChapterSelect()
    {
        var chapters = Vm.ChapterManager.GetAvailableChapters();
        const int chapterStartY = 200;

        for (int i = 2000; i < 2100; i++)
        {
            State.Render.Sprites.Remove(i);
            State.Interaction.SpriteButtonMap.Remove(i);
        }

        for (int i = 0; i < chapters.Count; i++)
        {
            int cardId = 2000 + i;
            var chapter = chapters[i];
            bool isUnlocked = chapter.IsUnlocked;
            if (State.FlagRuntime.Flags.TryGetValue($"chapter{chapter.Id}_unlocked", out bool flagValue))
            {
                isUnlocked = flagValue;
            }

            int y = chapterStartY + (i * 120);
            State.Render.Sprites[cardId] = new Sprite
            {
                Id = cardId,
                Type = SpriteType.Rect,
                X = 340,
                Y = y,
                Width = 600,
                Height = 100,
                FillColor = isUnlocked ? "#2a2a3e" : "#1a1a2e",
                FillAlpha = 255,
                IsButton = isUnlocked,
                CornerRadius = 12,
                BorderColor = isUnlocked ? "#4a4a6e" : "#2a2a4e",
                BorderWidth = 2,
                ShadowColor = "#000000",
                ShadowOffsetX = 4,
                ShadowOffsetY = 4,
                HoverFillColor = "#3a3a5e",
                HoverScale = 1.02f
            };

            State.Render.Sprites[cardId + 1] = new Sprite
            {
                Id = cardId + 1,
                Type = SpriteType.Text,
                Text = chapter.Title,
                X = 360,
                Y = y + 25,
                FontSize = 24,
                Color = isUnlocked ? "#ffffff" : "#666688",
                TextShadowColor = "#000000",
                TextShadowX = 2,
                TextShadowY = 2
            };

            State.Render.Sprites[cardId + 2] = new Sprite
            {
                Id = cardId + 2,
                Type = SpriteType.Text,
                Text = chapter.Description,
                X = 360,
                Y = y + 60,
                FontSize = 16,
                Color = isUnlocked ? "#aaaaee" : "#555577"
            };

            if (isUnlocked) State.Interaction.SpriteButtonMap[cardId] = chapter.Id;
        }
    }

    private void ExecuteChapterCard(Instruction inst)
    {
        int cardId = GetVal(inst.Arguments[0]);
        string title = GetString(inst.Arguments[1]);
        string description = inst.Arguments.Count > 2 ? GetString(inst.Arguments[2]) : "";
        int x = GetVal(inst.Arguments[3]);
        int y = GetVal(inst.Arguments[4]);

        State.Render.Sprites[cardId] = new Sprite
        {
            Id = cardId,
            Type = SpriteType.Rect,
            X = x,
            Y = y,
            Width = 600,
            Height = 100,
            FillColor = "#333333",
            FillAlpha = 255,
            IsButton = true
        };

        State.Render.Sprites[cardId + 1] = new Sprite
        {
            Id = cardId + 1,
            Type = SpriteType.Text,
            Text = title,
            X = x + 20,
            Y = y + 20,
            FontSize = 24,
            Color = "#ffffff"
        };

        if (!string.IsNullOrEmpty(description))
        {
            State.Render.Sprites[cardId + 2] = new Sprite
            {
                Id = cardId + 2,
                Type = SpriteType.Text,
                Text = description,
                X = x + 20,
                Y = y + 55,
                FontSize = 16,
                Color = "#aaaaaa"
            };
        }
    }

    private static GameScene ParseScene(string scene)
    {
        return scene.ToLowerInvariant() switch
        {
            "titlescreen" => GameScene.TitleScreen,
            "chapterselect" => GameScene.ChapterSelect,
            "gameplay" => GameScene.GamePlay,
            "systemmenu" => GameScene.SystemMenu,
            "saveloadmenu" => GameScene.SaveLoadMenu,
            "settings" => GameScene.Settings,
            "gallery" => GameScene.Gallery,
            _ => GameScene.TitleScreen
        };
    }
}
