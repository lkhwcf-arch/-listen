using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using EscapeGame.Data;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Infrastructure;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Presentation;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.CsvValidation.Schema;

namespace EscapeGame.Editor
{
    // 작은 에디터 View와 전용 Controller만 둡니다. 검증·등록 로직은 일반 C# 데이터 모듈을 사용합니다.
    public sealed class CsvValidatorWindow : EditorWindow
    {
        private CsvValidatorController controller;
        private Vector2 scroll;
        private bool showWarnings = true, checkResources = true;
        private string search = "";

        [MenuItem("Tools/CSV/검사기")]
        public static void Open() => GetWindow<CsvValidatorWindow>("CSV 검사기");
        private void OnEnable()
        {
            controller = new CsvValidatorController(Path.GetDirectoryName(Application.dataPath));
            controller.Changed += Repaint;
        }
        private void OnDisable()
        {
            if (controller == null) return;
            controller.Changed -= Repaint;
            controller.Dispose(); controller = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Assets/11.CSV", EditorStyles.boldLabel);
            if (controller == null) return;
            using (new EditorGUI.DisabledScope(controller.IsBusy))
            {
                checkResources = EditorGUILayout.ToggleLeft("프리팹·사운드 경로 확인 (미발견은 경고)", checkResources);
                if (GUILayout.Button("메모리 데이터 검사", GUILayout.Height(28))) Run(false);
                if (GUILayout.Button("CSV 다시 읽고 검사")) Run(true);
            }
            if (controller.IsBusy) EditorGUILayout.HelpBox("CSV 검사 중입니다.", MessageType.Info);
            if (controller.Failure != null) EditorGUILayout.HelpBox(controller.Failure, MessageType.Error);
            var report = controller.Report;
            if (report == null)
            {
                EditorGUILayout.HelpBox("처음 검사할 때 CSV를 읽습니다. 파일 수정 후에는 다시 읽기를 선택하세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox($"{report.FileCount}개 파일 · 오류 {report.ErrorCount}개 · 경고 {report.WarningCount}개",
                report.IsValid ? MessageType.Info : MessageType.Error);
            showWarnings = EditorGUILayout.ToggleLeft("경고 표시", showWarnings);
            search = EditorGUILayout.TextField("검색", search);
            if (GUILayout.Button("전체 결과 복사")) EditorGUIUtility.systemCopyBuffer = string.Join("\n", report.Issues.Select(CsvValidationMessages.Format));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var issue in report.Issues)
            {
                if (!showWarnings && issue.Severity == CsvValidationSeverity.Warning) continue;
                if (search.Length > 0 && CsvValidationMessages.Format(issue).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{issue.Severity} · {issue.Sheet}.csv · {issue.Line}행 · {issue.Column}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(CsvValidationMessages.Describe(issue), EditorStyles.wordWrappedLabel);
                if (GUILayout.Button("CSV 열기", GUILayout.Width(90))) controller.Open(issue);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
        private async void Run(bool reload)
        {
            scroll = Vector2.zero;
            await controller.ValidateAsync(reload, checkResources);
        }
    }

    internal sealed class CsvValidatorController : IDisposable
    {
        private readonly string folder;
        private readonly string rulesPath;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly CsvDataStore store = new CsvDataStore();
        private readonly Utf8CsvFileSource source = new Utf8CsvFileSource();
        private CsvDataSet data;
        private CsvValidationCatalog catalog;
        private bool disposed;
        public bool IsBusy { get; private set; }
        public string Failure { get; private set; }
        public CsvValidationReport Report { get; private set; }
        public event Action Changed;

        public CsvValidatorController(string root)
        {
            folder = Path.Combine(root, "Assets/11.CSV");
            rulesPath = Path.Combine(root, "Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml");
        }
        public async Task ValidateAsync(bool reload, bool checkResources)
        {
            if (disposed || IsBusy) return;
            IsBusy = true; Failure = null; Changed?.Invoke();
            var token = lifetime.Token;
            try
            {
                if (catalog == null || reload) catalog = CsvValidationCatalog.Read(await source.ReadTextAsync(rulesPath, token));
                if (data == null || reload)
                {
                    var files = Directory.GetFiles(folder).Where(path => string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
                        .Select(path => new System.Collections.Generic.KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(path), path));
                    data = await CsvDataSet.LoadAsync(files, new CsvLoader(source, new CsvParser()), token);
                }
                token.ThrowIfCancellationRequested();
                var validator = new CsvValidator(catalog, checkResources ? new UnityCsvResourceResolver() : null);
                store.TryRegister(data, validator, out var report, token);
                Report = report; // 오류가 있는 후보도 화면에서는 보여주지만 저장소에는 등록하지 않습니다.
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (!disposed) { Failure = "CSV 검사를 실행하지 못했습니다: " + error.Message; Debug.LogWarning(error); }
            }
            finally { IsBusy = false; if (!disposed) Changed?.Invoke(); }
        }
        public void Open(CsvValidationIssue issue)
        {
            if (disposed || Report == null || !Report.Issues.Contains(issue) || !Directory.Exists(folder)) return;
            string file = Directory.GetFiles(folder).FirstOrDefault(path => string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(path) == issue.Sheet);
            if (file != null) UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(file, Math.Max(1, issue.Line));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; lifetime.Cancel(); lifetime.Dispose(); Changed = null;
        }
    }
}
