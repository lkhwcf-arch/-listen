using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data.Csv;
using EscapeGame.Data.CsvValidation.Contracts;
using EscapeGame.Data.CsvValidation.Runtime;

namespace EscapeGame.Data
{
    // 호출 소유자(Unity에서는 Bootstrapper)가 보유하고 주입합니다. 전역 Singleton은 만들지 않습니다.
    public sealed class CsvDataStore
    {
        private readonly SemaphoreSlim initialization = new SemaphoreSlim(1, 1);
        private readonly Dictionary<(string Sheet, Type Type), object> mappedTables = new Dictionary<(string, Type), object>();
        public CsvDataSet Data
        {
            get;
            private set;
        }
        public CsvValidationReport Validation
        {
            get;
            private set;
        }
        public bool HasData => Data != null;
        public event Action<CsvDataSet> Registered;

        // 최초 성공한 등록 이후에는 load를 다시 호출하지 않습니다. 대기 중인 호출도 중복 로드하지 않습니다.
        public async Task<CsvValidationReport> InitializeAsync(Func<CancellationToken, Task<CsvDataSet>> load, CsvValidator validator, CancellationToken token)
        {
            if (load == null)
                throw new ArgumentNullException(nameof(load));
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));
            token.ThrowIfCancellationRequested();
            if (HasData)
                return Validation;
            await initialization.WaitAsync(token);
            try
            {
                if (HasData)
                    return Validation;
                var data = await load(token);
                token.ThrowIfCancellationRequested();
                if (HasData)
                    return Validation; // 대기 중 소유자가 직접 등록한 최신 데이터를 덮어쓰지 않습니다.
                TryRegister(data, validator, out var report, token);
                return report;
            }
            finally
            {
                initialization.Release();
            }
        }

        // 명시적인 재등록도 검증을 거칩니다. 실패한 데이터로 기존 정상 데이터를 덮어쓰지 않습니다.
        public bool TryRegister(CsvDataSet data, CsvValidator validator, out CsvValidationReport report, CancellationToken token = default)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (validator == null)
                throw new ArgumentNullException(nameof(validator));
            report = validator.Validate(data, token);
            if (!report.IsValid)
                return false;
            token.ThrowIfCancellationRequested();
            if (ReferenceEquals(Data, data))
            {
                Validation = report;
                return true;
            }
            Data = data;
            Validation = report;
            mappedTables.Clear(); // 새 원본이 등록되면 이전 원본의 변환 캐시는 더 이상 사용하지 않습니다.
            Registered?.Invoke(data); // 상태가 모두 반영된 후 알립니다.
            return true;
        }

        // 실제 데이터 클래스의 생성 방식은 사용자가 제공하는 변환 함수가 결정합니다.
        // 같은 시트/타입은 한 번만 변환하고 이후 GetTable로 재사용합니다. 반환된 T의 가변성은 T의 설계에 따릅니다.
        public IReadOnlyList<T> MapTable<T>(string sheet, Func<CsvRow, T> map)
        {
            if (!HasData)
                throw new InvalidOperationException("Register valid CSV data before mapping tables.");
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            var key = (sheet, typeof(T));
            if (mappedTables.ContainsKey(key))
                throw new InvalidOperationException("CSV table/type is already mapped: " + sheet);
            var source = Data;
            var converted = Array.AsReadOnly(source.GetDocument(sheet).Rows.Select(map).ToArray());
            // 변환 중 예외가 나면 캐시에는 아무것도 등록하지 않습니다.
            if (!ReferenceEquals(source, Data))
                throw new InvalidOperationException("CSV data changed during mapping.");
            mappedTables.Add(key, converted);
            return converted;
        }

        public IReadOnlyList<T> GetTable<T>(string sheet)
            => mappedTables.TryGetValue((sheet, typeof(T)), out var table) ? (IReadOnlyList<T>)table
                : throw new KeyNotFoundException("CSV table/type has not been mapped: " + sheet);

        public IReadOnlyList<T> MapTable<T>(CsvTableMapping<T> mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));
            return MapTable(mapping.SheetName, mapping.Convert);
        }

        public IReadOnlyList<T> GetTable<T>(CsvTableMapping<T> mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));
            return GetTable<T>(mapping.SheetName);
        }

        public bool TryGetTable<T>(CsvTableMapping<T> mapping, out IReadOnlyList<T> table)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));
            bool found = mappedTables.TryGetValue((mapping.SheetName, typeof(T)), out var value);
            table = found ? (IReadOnlyList<T>)value : null;
            return found;
        }
    }
}
