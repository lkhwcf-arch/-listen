using System;
using System.Collections.Generic;

namespace EscapeGame.Data.Tables
{
    // 저장소의 동일 원본에서 만든 테이블 묶음입니다. 소비자에게는 필요한 테이블만 주입합니다.
    public sealed class CsvGameData
    {
        public CsvDataSet Source { get; }
        public IReadOnlyList<AnchorInteractData> InteractAnchors { get; }
        public IReadOnlyList<AnchorNonInteractData> NonInteractAnchors { get; }
        public IReadOnlyList<ArrowData> Arrows { get; }
        public IReadOnlyList<CardData> Cards { get; }
        public IReadOnlyList<CharacterStatData> CharacterStats { get; }
        public IReadOnlyList<ColorLetterData> ColorLetters { get; }
        public IReadOnlyList<ComponentData> Components { get; }
        public IReadOnlyList<ConsoleData> Consoles { get; }
        public IReadOnlyList<FormulaData> Formulas { get; }
        public IReadOnlyList<FrameData> Frames { get; }
        public IReadOnlyList<GameConfigData> GameConfigs { get; }
        public IReadOnlyList<LockData> Locks { get; }
        public IReadOnlyList<PaperData> Papers { get; }
        public IReadOnlyList<PrefabData> Prefabs { get; }
        public IReadOnlyList<PuzzleSlotData> PuzzleSlots { get; }
        public IReadOnlyList<PuzzleData> Puzzles { get; }
        public IReadOnlyList<RegistryData> Registry { get; }
        public IReadOnlyList<RoomAData> RoomsA { get; }
        public IReadOnlyList<RoomBData> RoomsB { get; }
        public IReadOnlyList<SoundLinkData> SoundLinks { get; }
        public IReadOnlyList<SoundData> Sounds { get; }
        public IReadOnlyList<TvData> Tvs { get; }
        public IReadOnlyList<WireData> Wires { get; }

        public CsvGameData(CsvDataStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            Source = store.Data ?? throw new InvalidOperationException("Register valid CSV data before creating game data.");
            InteractAnchors = Resolve(store, AnchorInteractData.Table);
            NonInteractAnchors = Resolve(store, AnchorNonInteractData.Table);
            Arrows = Resolve(store, ArrowData.Table);
            Cards = Resolve(store, CardData.Table);
            CharacterStats = Resolve(store, CharacterStatData.Table);
            ColorLetters = Resolve(store, ColorLetterData.Table);
            Components = Resolve(store, ComponentData.Table);
            Consoles = Resolve(store, ConsoleData.Table);
            Formulas = Resolve(store, FormulaData.Table);
            Frames = Resolve(store, FrameData.Table);
            GameConfigs = Resolve(store, GameConfigData.Table);
            Locks = Resolve(store, LockData.Table);
            Papers = Resolve(store, PaperData.Table);
            Prefabs = Resolve(store, PrefabData.Table);
            PuzzleSlots = Resolve(store, PuzzleSlotData.Table);
            Puzzles = Resolve(store, PuzzleData.Table);
            Registry = Resolve(store, RegistryData.Table);
            RoomsA = Resolve(store, RoomAData.Table);
            RoomsB = Resolve(store, RoomBData.Table);
            SoundLinks = Resolve(store, SoundLinkData.Table);
            Sounds = Resolve(store, SoundData.Table);
            Tvs = Resolve(store, TvData.Table);
            Wires = Resolve(store, WireData.Table);
            if (!ReferenceEquals(Source, store.Data)) throw new InvalidOperationException("CSV data changed while creating game data.");
        }

        private static IReadOnlyList<T> Resolve<T>(CsvDataStore store, CsvTableMapping<T> mapping)
        {
            if (store.TryGetTable(mapping, out var table)) return table;
            // REGISTRY에서 비활성화되었거나 선택 사항인 테이블은 없을 수 있습니다.
            if (!store.Data.Documents.ContainsKey(mapping.SheetName)) return Array.AsReadOnly(Array.Empty<T>());
            return store.MapTable(mapping);
        }
    }
}
