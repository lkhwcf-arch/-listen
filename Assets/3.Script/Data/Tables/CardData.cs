using EscapeGame.Data.Csv;

namespace EscapeGame.Data.Tables
{
    // CARD.csv의 한 행을 나타내는 읽기 전용 정의입니다.
    public sealed class CardData
    {
        public static CsvTableMapping<CardData> Table
        {
            get;
        } = new CsvTableMapping<CardData>
            ("CARD", row => new CardData(row));
        public int Id
        {
            get;
        }
        public int AnchorRef
        {
            get;
        }
        public int CardNo
        {
            get;
        }
        public string TopMark
        {
            get;
        }
        public string BottomMark
        {
            get;
        }
        public bool IsAnswer
        {
            get;
        }

        public CardData(CsvRow row)
        {
            var value = new CsvRowReader(row, Table.SheetName);
            Id = value.Integer("ID");
            AnchorRef = value.Integer("AnchorRef");
            CardNo = value.Integer("CardNo");
            TopMark = value.Text("TopMark");
            BottomMark = value.Text("BottomMark");
            IsAnswer = value.Flag("IsAnswer");
        }
    }
}
