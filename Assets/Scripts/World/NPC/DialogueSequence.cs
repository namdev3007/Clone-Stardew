using System;
using System.Collections.Generic;
using UnityEngine;

namespace World.NPC
{
    [CreateAssetMenu(fileName = "Dialogue Sequence", menuName = "NPC/Dialogue Sequence")]
    public sealed class DialogueSequence : ScriptableObject
    {
        public enum Speaker
        {
            Player,
            Npc
        }

        [Serializable]
        public sealed class Line
        {
            public Speaker speaker;
            public string localizationKey;
            [TextArea(2, 6)] public string english;
            [TextArea(2, 6)] public string vietnamese;

            public string GetText(bool useVietnamese)
            {
                if (useVietnamese)
                {
                    if (!string.IsNullOrWhiteSpace(vietnamese))
                        return vietnamese;

                    // Older dialogue assets were authored with empty Vietnamese
                    // fields. Keep a built-in fallback so selecting Vietnamese in
                    // Settings takes effect even before those assets are regenerated.
                    string builtInVietnamese = GetBuiltInVietnamese(localizationKey);
                    if (!string.IsNullOrWhiteSpace(builtInVietnamese))
                        return builtInVietnamese;
                }
                return english ?? string.Empty;
            }
        }

        [SerializeField] private string sequenceId;
        [SerializeField] private List<Line> lines = new List<Line>();

        public string SequenceId => sequenceId;
        public IReadOnlyList<Line> Lines => lines;

        public void Configure(string id, IEnumerable<Line> dialogueLines)
        {
            sequenceId = id;
            lines = new List<Line>(dialogueLines ?? Array.Empty<Line>());
        }

        public static string GetBuiltInVietnamese(string key)
        {
            switch (key)
            {
                case "grandpa.intro.001": return "Dậy rồi đấy à? Nắng lên đến đọt cây rồi kia kìa. Ở trên phố quen ngủ nướng rồi phải không?";
                case "grandpa.intro.002": return "Dạ... tại dạo này cháu mất ngủ. Lâu lắm rồi cháu mới ngủ được một giấc dài như vậy.";
                case "grandpa.intro.003": return "Gió quê mát nên dễ ngủ đấy. Nhưng thôi, trời sáng lâu rồi, ông có chút việc cho cháu đây.";
                case "grandpa.intro.004": return "Này, cầm lấy cái nón, cái cuốc và bịch hạt giống này. Nhớ đội nón vào, nắng ngoài này gắt hơn cháu nhớ đấy.";
                case "grandpa.intro.005": return "Làm luôn bây giờ hả ông?";
                case "grandpa.intro.006": return "Chứ cháu muốn đợi đến trưa nắng chang chang mới làm à?";
                case "grandpa.intro.007": return "Cỏ dại mọc lấn hết mảnh đất bên hông nhà rồi. Cháu dọn bớt cỏ và xới đất cho tơi ra nhé.";
                case "grandpa.intro.008": return "Cắm chắc lưỡi cuốc xuống rồi kéo ngược về phía mình. Đừng nghĩ nhiều, trước mắt cuốc thử ba ô đất đi.";
                case "grandpa.reminder.001": return "Cứ làm từng bước một thôi. Mảnh đất bên hông nhà là chỗ thích hợp để bắt đầu.";
                case "grandpa.reminder.002": return "Trước mắt cuốc ba ô là được. Nhớ để ý chỗ cháu vung cuốc đấy.";
                case "grandpa.after_hoe.001": return "Khá lắm. Tay chân cháu vẫn còn nhớ việc hơn cháu tưởng đấy.";
                case "grandpa.after_hoe.002": return "Này, ông hỏi thật. Trên thành phố rốt cuộc có chuyện gì mà cháu lại đột ngột về đây thế?";
                case "grandpa.after_hoe.003": return "Dạ... cũng không có gì đâu ông. Dạo này công ty ít việc nên cháu xin nghỉ vài hôm về thăm ông thôi.";
                case "grandpa.after_hoe.004": return "Cháu giấu ai chứ giấu sao nổi ông.";
                case "grandpa.after_hoe.005": return "Nhìn quầng thâm dưới mắt cháu, rồi nghe cháu thở dài từ sáng đến giờ là ông biết rồi.";
                case "grandpa.after_hoe.006": return "Lại làm bù đầu bù cổ, hay bị người ta chèn ép mà lương chẳng được bao nhiêu, đúng không?";
                case "grandpa.after_hoe.007": return "...";
                case "grandpa.after_hoe.008": return "Bằng tuổi cháu bây giờ, ông cũng từng nghĩ mình sức dài vai rộng thì việc gì cũng gánh được.";
                case "grandpa.after_hoe.009": return "Ông từng đi thanh niên xung phong, rồi làm ở hợp tác xã, lúc nào cũng muốn chứng minh rằng mình chịu được mọi thứ.";
                case "grandpa.after_hoe.010": return "Nhưng đời đâu có đơn giản như vậy.";
                case "grandpa.after_hoe.011": return "Người trẻ cứ mải chạy về phía trước, lúc nào cũng muốn nhanh hơn và chứng tỏ bản thân.";
                case "grandpa.after_hoe.012": return "Đến khi vấp ngã thì lại vì sĩ diện mà ôm hết buồn bực vào người.";
                case "grandpa.after_hoe.013": return "Cháu chỉ cảm thấy mình đang bị tụt lại phía sau thôi ông ạ.";
                case "grandpa.after_hoe.014": return "Bạn bè cháu đứa nào cũng thăng chức, mua nhà mua xe, còn cháu thì... lại quay về vạch xuất phát.";
                case "grandpa.after_hoe.015": return "Đừng lấy cuộc đời mình ra so với người khác.";
                case "grandpa.after_hoe.016": return "Mỗi người có một vạch xuất phát và một nhịp sống khác nhau.";
                case "grandpa.after_hoe.017": return "Cháu cứ đòi nhảy thẳng lên đỉnh thì chỉ có ngã đau hơn thôi.";
                case "grandpa.after_hoe.018": return "Vậy cháu nên làm gì hả ông?";
                case "grandpa.after_hoe.019": return "Nhìn mảnh đất này xem. Không phải cứ ném hạt xuống là sáng hôm sau có rau để hái đâu.";
                case "grandpa.after_hoe.020": return "Đầu tiên phải chuẩn bị một mảnh đất tốt, rồi mới gieo hạt. Sau đó mỗi ngày chăm nó một chút.";
                case "grandpa.after_hoe.021": return "Con người cũng chẳng khác là bao.";
                case "grandpa.after_hoe.022": return "Cháu đang kiệt sức, đầu óc lại rối bời. Cứ cho mình thời gian nghỉ ngơi rồi hãy quyết định sẽ đi đâu tiếp.";
                case "grandpa.after_hoe.023": return "Đi chậm không có nghĩa là thất bại. Cháu vẫn đang trưởng thành từng ngày đấy thôi.";
                case "grandpa.after_hoe.024": return "Dạ, cháu hiểu rồi.";
                case "grandpa.after_hoe.025": return "Tốt. Giờ cứ ăn uống, ngủ nghỉ cho khỏe rồi làm đất theo sức của mình.";
                case "grandpa.after_hoe.026": return "Khi cần hạt giống hay nông cụ thì sang tìm chú Hải. Chú ấy tròn tròn ở gần đây, cháu nhìn là nhận ra ngay.";
                case "grandpa.after_hoe.027": return "À, để ý quanh vườn nhé. Ông để quên cái bình tưới ở đâu đó ngoài kia rồi.";
                case "grandpa.after_hoe.028": return "Dạ, cháu nhớ rồi ông.";
                case "grandpa.farming_advice.001": return "Cải tạo đất trước, gieo hạt xuống rồi kiên nhẫn chờ cây lớn.";
                case "grandpa.farming_advice.002": return "Chăm sóc tốt là cần, nhưng kiên nhẫn mới là việc quan trọng nhất.";
                case "grandpa.farming_advice.003": return "Dạ, cháu nhớ rồi ông.";
                case "grandpa.expand_unavailable.001": return "Cứ lo cho mảnh ruộng đầu tiên ổn thỏa đã rồi mình mới tính chuyện khai thêm đất.";
                case "grandpa.expand_unavailable.002": return "Khi nào trong làng giải quyết lại được giấy phép và tiền mở đất thì cháu quay lại nhé.";
                case "hai.intro.001": return "Dạ chào chú. Cháu là cháu nội ông Tám ở xóm dưới, mới về hôm qua ạ.";
                case "hai.intro.002": return "Ơ! Cháu lão Tám đấy à? Trông lớn thế này rồi cơ à!";
                case "hai.intro.003": return "Về từ hôm qua mà giờ mới chịu sang chào chú đấy nhé?";
                case "hai.intro.004": return "Da trắng, tay sạch thế này đúng là dân thành phố rồi!";
                case "hai.intro.005": return "Sao, sang đây mua gì? Rượu cho ông nội hay bánh kẹo cho mình?";
                case "hai.intro.006": return "Dạ không chú. Ông nội bảo cháu sang mua ít hạt giống với dụng cụ làm vườn ạ.";
                case "hai.intro.007": return "Cháu định trồng rau à? Tốt!";
                case "hai.intro.008": return "Thanh niên giờ chịu đụng tay vào đất cát là quý rồi đấy.";
                case "hai.intro.009": return "Còn hơn cứ cắm mặt vào điện thoại cả ngày đến mụ mị người.";
                case "hai.intro.010": return "Chú có hạt giống, nông cụ và đủ thứ cần cho một khu vườn nhỏ. Cháu vào xem đi.";
                case "hai.revisit.001.001": return "Lại mua hạt giống hả cháu? Hay lần này định sắm thêm ít đồ nghề?";
                case "hai.revisit.002.001": return "Cái cuốc dùng có êm không, hay cháu đã làm cong nó mất rồi?";
                case "hai.revisit.003.001": return "Đợi chú tỉa nốt cành này nhé. Cháu cần gì thì cứ vào xem trước đi.";
                case "hai.revisit.004.001": return "Ông cháu ngày trước mua một hạt cũng phải mặc cả thành ba. Đừng học cái tính ấy nhé.";
                case "hai.revisit.005.001": return "Làm nông thì tưởng đơn giản, cho đến khi thời tiết tự dưng đổi ý đấy.";
                default: return string.Empty;
            }
        }
    }
}
