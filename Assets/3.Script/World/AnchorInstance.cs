using EscapeGame.Data.Tables;
using UnityEngine;

namespace EscapeGame.World
{
    public interface IAnchorDataReceiver
    {
        void Initialize(AnchorData anchor, CsvGameData data);
    }

    // 스폰 객체는 문자열로 CSV를 다시 검색하지 않고 주입된 정의와 데이터 묶음을 사용합니다.
    public sealed class AnchorInstance : MonoBehaviour
    {
        public AnchorData Definition
        {
            get;
            private set;
        }
        public CsvGameData Data
        {
            get;
            private set;
        }
        public void Initialize(AnchorData definition, CsvGameData data)
        {
            Definition = definition;
            Data = data;
            foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
                if (component is IAnchorDataReceiver receiver)
                    receiver.Initialize(definition, data);
        }
    }
}
