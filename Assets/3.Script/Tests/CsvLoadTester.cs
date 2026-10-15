using System;
using System.Threading;
using EscapeGame.Data.Csv;
using EscapeGame.Data.Infrastructure;
using UnityEngine;

namespace EscapeGame.Tests
{
    // 실제 테이블이 준비되면 CSV를 연결해 수동으로 로딩을 확인합니다. 자동 실행하지 않습니다.
    public sealed class CsvLoadTester : MonoBehaviour
    {
        [SerializeField] private TextAsset csvAsset;
        private bool loading;
        public CsvDocument Document { get; private set; }

        [ContextMenu("Load CSV")]
        private async void LoadCsv()
        {
            if (loading) return;
            if (csvAsset == null) { Debug.LogError("[CSV] Csv Asset에 CSV 파일을 연결하세요."); return; }
            loading = true;
            Document = null;
            var token = Application.isPlaying ? destroyCancellationToken : CancellationToken.None;
            try
            {
                var loader = new CsvLoader(new UnityTextAssetCsvSource(csvAsset), new CsvParser());
                var loaded = await loader.LoadAsync(csvAsset.name, token);
                token.ThrowIfCancellationRequested();
                Document = loaded;
                Debug.Log($"[CSV] 로드 완료: {loaded.SourceName}, 헤더 {loaded.Headers.Count}개, 데이터 {loaded.Rows.Count}행");
                Debug.Log("[CSV] 헤더: " + string.Join(", ", loaded.Headers));
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogError("[CSV] " + exception.Message); }
            finally { loading = false; }
        }
    }
}
