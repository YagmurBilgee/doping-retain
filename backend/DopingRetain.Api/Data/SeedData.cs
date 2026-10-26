using DopingRetain.Api.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DopingRetain.Api.Data;

/// <summary>
/// Demo content and the demo student's initial scenario. Five topics from five subjects each get one
/// SYNTHETIC normal-test observation (a made-up demo score, not a real student result). Its completion time
/// is derived backwards from the moment its curve should reach the review threshold, so right after a seed
/// or an explicit demo reset three topics are due today, one is scheduled for tomorrow and one for three
/// days later. Days follow the student's Europe/Istanbul calendar; every timestamp is stored in UTC.
/// </summary>
public static class SeedData
{
    public const string DemoStudentId = "demo-student";

    /// <summary>The demo calendar: "today" is the reference instant's date in Istanbul.</summary>
    private static readonly TimeZoneInfo DemoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>Local hour at which the future topics' reviews become due.</summary>
    private const int FutureReviewHour = 10;

    /// <param name="Test">The demo student's synthetic normal test; null for content not in the demo scenario.</param>
    private sealed record SeedTopic(
        string Id,
        string Title,
        string Category,
        SeedTest? Test,
        QuizQuestion[] Questions,
        SummaryPoint[]? SummaryPoints = null);

    /// <summary>A synthetic normal-test score and when its curve should reach the review threshold.</summary>
    private sealed record SeedTest(int CorrectCount, int TotalQuestions, ReviewTarget Target);

    /// <summary>When a seeded test's curve reaches the review threshold, on the demo calendar.</summary>
    private abstract record ReviewTarget;

    /// <summary>
    /// Earlier today, at this fraction of the time from today's 00:00 to the reference instant: already due,
    /// even just after midnight. Smaller fractions cross earlier, so they come first in the due list.
    /// </summary>
    private sealed record DueEarlierToday(double FractionOfDaySoFar) : ReviewTarget;

    /// <summary>At <see cref="FutureReviewHour"/>:00 local time, this many days after today.</summary>
    private sealed record DueOnDay(int DaysFromToday) : ReviewTarget;

    private static readonly SeedTopic[] Topics =
    [
        new("ayt-mat-trigonometri", "Trigonometri", "AYT Matematik", new SeedTest(18, 20, new DueEarlierToday(0.25)),
        [
            Question("trig-q1", "ayt-mat-trigonometri", 1,
                "sin 30° · cos 60° + cos 30° · sin 60° işleminin sonucu kaçtır?",
                ["1/2", "√3/2", "0", "1"], correctIndex: 3,
                "sin 30° · cos 60° + cos 30° · sin 60° = sin(30° + 60°) = sin 90° = 1"),
            Question("trig-q2", "ayt-mat-trigonometri", 2,
                "x dar açı ve tan x = 3/4 olduğuna göre, sin x kaçtır?",
                ["3/5", "4/5", "3/4", "5/3"], correctIndex: 0,
                "tan x = 3/4 → kenarlar 3 ve 4, hipotenüs 5 (3-4-5 üçgeni) → sin x = karşı / hipotenüs = 3/5"),
            Question("trig-q3", "ayt-mat-trigonometri", 3,
                "cos 2x ifadesi aşağıdakilerden hangisine eşittir?",
                ["2 · sin x · cos x", "1 − 2sin²x", "sin²x + cos²x", "2cos²x + 1"], correctIndex: 1,
                "cos 2x = cos²x − sin²x; cos²x yerine 1 − sin²x yazılırsa cos 2x = 1 − 2sin²x"),
        ],
        [
            new("📌", "Temel Özdeşlik", "sin²x + cos²x = 1", "Kareler toplamı daima 1’dir.",
                Detail: "Birim çemberde bir açının kenarı çemberi (cos x, sin x) noktasında keser. Bu noktanın merkeze uzaklığı 1 olduğundan Pisagor bağıntısı cos²x + sin²x = 1 sonucunu verir. Özdeşlik her x gerçek sayısı için geçerlidir. Sinüs ya da kosinüsten biri bilindiğinde diğerinin karesi bulunur; işaret ise açının bulunduğu bölgeye göre seçilir.",
                Example: "x dar açı ve sin x = 3/5 olsun. cos²x = 1 − 9/25 = 16/25 olur; dar açıda kosinüs pozitif olduğundan cos x = 4/5 bulunur."),
            new("💡", "Oran Kuralı", "tan x = sin x / cos x",
                Detail: "Tanjant, aynı açının sinüsünün kosinüsüne oranıdır; dik üçgende karşı dik kenarın komşu dik kenara oranına karşılık gelir. Bölme yapıldığı için kural yalnızca cos x ≠ 0 iken geçerlidir; örneğin x = 90° için tan x tanımsızdır. Kotanjant bu oranın tersidir: sin x ≠ 0 iken cot x = cos x / sin x. Tanjant verildiğinde kenarlara k katsayısıyla değer verip hipotenüsü bulmak, sinüs ve kosinüse ulaşmanın en kısa yoludur.",
                Example: "x dar açı ve tan x = 3/4 olsun. Karşı kenar 3k, komşu kenar 4k alınır; hipotenüs √(9k² + 16k²) = 5k olur. Buradan sin x = 3/5 ve cos x = 4/5 bulunur; kontrol: (3/5) / (4/5) = 3/4."),
            new("⚡", "Sık Karıştırılan", "sin 30° = cos 60° = 1/2",
                Detail: "Toplamı 90° olan (tümler) iki açıdan birinin sinüsü diğerinin kosinüsüne eşittir: sin x = cos(90° − x). 30° ile 60° tümler olduğu için sin 30° = cos 60° = 1/2 ve sin 60° = cos 30° = √3/2 olur. Dar açılarda açı büyüdükçe sinüs artar, kosinüs azalır; bu yüzden küçük açı olan 30°’nin sinüsü küçük değer olan 1/2’dir. Değerleri ezberlerken bu sıralamayı kontrol olarak kullanabilirsin.",
                Example: "sin 30° · cos 60° + cos 30° · sin 60° ifadesi sin(a + b) = sin a · cos b + cos a · sin b kalıbındadır. Sonuç sin(30° + 60°) = sin 90° = 1 olur; değerler tek tek yazılırsa da 1/4 + 3/4 = 1 çıkar."),
        ]),
        new("tyt-bio-hucre-bolunmeleri", "Hücre Bölünmeleri", "TYT Biyoloji", new SeedTest(16, 20, new DueEarlierToday(0.5)),
        [
            Question("bio-q1", "tyt-bio-hucre-bolunmeleri", 1,
                "Kromozom sayısı 2n = 46 olan bir hücre mitoz bölünme geçirdiğinde oluşan her yeni hücrenin kromozom sayısı kaçtır?",
                ["23", "46", "69", "92"], correctIndex: 1,
                "Mitozda kromozom sayısı korunur: 2n = 46 kromozomlu hücreden 46 kromozomlu iki hücre oluşur."),
            Question("bio-q2", "tyt-bio-hucre-bolunmeleri", 2,
                "Mayoz bölünme sonunda bir hücreden kaç hücre oluşur?",
                ["1", "2", "4", "8"], correctIndex: 2,
                "Mayozda art arda iki bölünme (mayoz I ve mayoz II) olur; bir hücreden kromozom sayısı yarıya inmiş 4 hücre oluşur."),
            Question("bio-q3", "tyt-bio-hucre-bolunmeleri", 3,
                "Krossing-over hangi evrede gerçekleşir?",
                ["Mitozun profaz evresinde", "Mayoz I'in profaz evresinde", "Mayoz II'nin metafaz evresinde", "Mitozun anafaz evresinde"], correctIndex: 1,
                "Krossing-over, mayoz I'in profazında homolog kromozomlar arasındaki parça değişimidir; mitozda görülmez."),
        ],
        [
            new("📌", "Mitoz", "Mitoz: 1 hücre → 2 hücre, kromozom sayısı korunur (2n → 2n)", "Büyüme, onarım ve eşeysiz üremeyi sağlar.",
                Detail: "Mitozdan önce DNA eşlenir ve her kromozom iki kardeş kromatitten oluşur. Bölünmede kardeş kromatitler ayrılarak iki yeni hücreye eşit dağıtılır. Sonuçta ana hücreyle aynı kromozom sayısına ve aynı kalıtsal yapıya sahip iki hücre oluşur. Çok hücrelilerde büyüme ve onarımı, bazı canlılarda eşeysiz üremeyi sağlar.",
                Example: "2n = 46 kromozomlu bir insan deri hücresi mitozla bölünürse 46 kromozomlu iki hücre oluşur. Bu hücreler bir kez daha bölünürse 4 hücre olur ve her biri yine 46 kromozomludur."),
            new("💡", "Mayoz", "Mayoz: 1 hücre → 4 hücre, kromozom sayısı yarıya iner (2n → n)", "Üreme hücreleri (gametler) bu bölünmeyle oluşur.",
                Detail: "Mayoz art arda iki bölünmeden oluşur ve üreme hücresi oluşturan hücrelerde görülür. Mayoz I’de homolog kromozomlar ayrılır ve kromozom sayısı yarıya iner (2n → n). Mayoz II’de kardeş kromatitler ayrılır; kromozom sayısı n olarak kalır. Sonuçta bir hücreden kalıtsal yapısı birbirinden farklı olabilen dört hücre oluşur.",
                Example: "2n = 46 kromozomlu bir hücre mayoz geçirirse 23 kromozomlu dört hücre oluşur. Döllenmede 23 kromozomlu iki gamet birleşince zigot yeniden 46 kromozomlu olur."),
            new("⚡", "Sık Karıştırılan", "Krossing-over mayoz I'in profaz evresinde olur", "Kalıtsal çeşitliliği artırır; mitozda görülmez.",
                Detail: "Mayoz I’in profaz evresinde homolog kromozomlar yan yana gelerek eşleşir. Bu sırada kardeş olmayan kromatitler arasında parça değişimi olabilir; buna krossing-over denir. Böylece aynı kromozomda yeni gen kombinasyonları oluşur ve kalıtsal çeşitlilik artar. Mitozda homolog kromozomlar eşleşmediği için krossing-over görülmez.",
                Example: "Soru: Krossing-over hangi evrede görülür? Cevap: profaz I (mayoz I). Mitozun profazı ve mayoz II yanlış seçeneklerdir, çünkü bu evrelerde homolog kromozomlar eşleşmez."),
        ]),
        new("tyt-tur-sozcukte-anlam", "Sözcükte Anlam", "TYT Türkçe", new SeedTest(19, 20, new DueOnDay(1)),
        [
            Question("tur-q1", "tyt-tur-sozcukte-anlam", 1,
                "“Keskin” sözcüğü hangi cümlede mecaz anlamda kullanılmıştır?",
                ["Keskin bıçakla ekmeği dilimledi.", "Keskin sözleriyle herkesi kırdı.", "Kalemin keskin ucu kâğıdı deldi.", "Bahçıvan keskin makasla dalları budadı."], correctIndex: 1,
                "“Keskin söz” kırıcı söz demektir; sözcük “iyi kesen” anlamından uzaklaştığı için mecazdır. Diğer cümlelerde gerçek anlamdadır."),
            Question("tur-q2", "tyt-tur-sozcukte-anlam", 2,
                "“Siyah” sözcüğünün eş anlamlısı hangisidir?",
                ["Beyaz", "Kara", "Mavi", "Gri"], correctIndex: 1,
                "Eş anlamlı sözcüklerin yazılışı farklı, anlamı aynıdır: siyah – kara. Beyaz ise siyahın zıt anlamlısıdır."),
            Question("tur-q3", "tyt-tur-sozcukte-anlam", 3,
                "“Uzun” sözcüğünün zıt anlamlısı hangisidir?",
                ["Kısa", "Geniş", "Büyük", "Yüksek"], correctIndex: 0,
                "Zıt anlamlı sözcükler anlamca birbirinin karşıtıdır: uzun – kısa."),
        ],
        [
            new("📌", "Gerçek Anlam", "Gerçek anlam: Sözcüğün akla gelen ilk anlamı", "Örnek: Keskin bıçakla ekmeği kesti.",
                Detail: "Gerçek anlam, sözcüğün sözlükteki ilk ve temel anlamıdır; sözcüğü duyunca akla ilk gelen anlam budur. Sözcük cümlede bu temel anlamıyla kullanılıyorsa gerçek anlamdadır. Anlamı belirleyen, sözcüğün tek başına ne olduğu değil, cümlede nasıl kullanıldığıdır. Bu yüzden aynı sözcük bir cümlede gerçek, başka bir cümlede mecaz anlamlı olabilir.",
                Example: "“Keskin bıçakla ekmeği kesti.” cümlesinde “keskin” iyi kesen anlamındadır. Bu, sözcüğün ilk anlamıdır; yani “keskin” burada gerçek anlamda kullanılmıştır."),
            new("💡", "Mecaz Anlam", "Mecaz anlam: Sözcüğün gerçek anlamından tamamen uzaklaşması", "Örnek: Keskin sözleri herkesi üzdü.",
                Detail: "Mecaz anlamda sözcük, gerçek anlamından tamamen uzaklaşarak yeni bir anlam kazanır. Çoğu zaman somut bir nitelik, duygu ya da düşünce gibi soyut bir kavrama aktarılır. Mecazı ayırt etmek için sözcüğün gerçek anlamını cümleye uygulamayı dene: uymuyorsa anlam mecazdır. Soğuk bakış, ağır söz ve tatlı dil sık karşılaşılan örneklerdir.",
                Example: "“Keskin sözleri herkesi üzdü.” cümlesinde sözler bir şeyi kesmez. “Keskin” burada kırıcı, sert anlamındadır; gerçek anlamından uzaklaştığı için mecazdır."),
            new("⚡", "Sık Karıştırılan", "Eş anlamlı: siyah – kara; zıt anlamlı: uzun – kısa", "Eş anlamlıların anlamı aynı, zıt anlamlıların anlamı karşıttır.",
                Detail: "Eş anlamlı sözcüklerin yazılışı farklı, anlamı aynıdır: siyah – kara, yanıt – cevap, okul – mektep. Zıt anlamlı sözcükler anlamca birbirinin karşıtıdır: uzun – kısa, sıcak – soğuk. Bir sözcüğün olumsuz biçimi zıt anlam sayılmaz; “güzel – güzel değil” değil, “güzel – çirkin” zıt anlamlıdır. Eş ve zıt anlamlılık da sözcüğün cümledeki anlamına göre değerlendirilir.",
                Example: "“Soruya kesin bir yanıt verdi.” cümlesinde “yanıt” yerine “cevap” yazılınca anlam değişmez; ikisi eş anlamlıdır. “Yol uzundu.” yerine “Yol kısaydı.” yazılınca anlam tersine döner; uzun – kısa zıt anlamlıdır."),
        ]),
        new("tyt-fiz-hareket-ve-kuvvet", "Hareket ve Kuvvet", "TYT Fizik", new SeedTest(11, 15, new DueEarlierToday(0.75)),
        [
            Question("fiz-q1", "tyt-fiz-hareket-ve-kuvvet", 1,
                "Kütlesi 2 kg olan bir cisme 10 N büyüklüğünde net kuvvet uygulanıyor. Cismin ivmesi kaç m/s²'dir?",
                ["5", "20", "8", "12"], correctIndex: 0,
                "a = F / m = 10 / 2 = 5 m/s²."),
            Question("fiz-q2", "tyt-fiz-hareket-ve-kuvvet", 2,
                "Hareket hâlindeki bir cisme etki eden net kuvvet sıfır olursa cisim için aşağıdakilerden hangisi doğrudur?",
                ["Yavaşlayarak durur", "Sabit hızla hareketine devam eder", "Hızlanır", "Hareket yönü sürekli değişir"], correctIndex: 1,
                "Net kuvvet sıfırsa ivme de sıfırdır; hareketli cisim sabit hızla gitmeye devam eder (eylemsizlik)."),
            Question("fiz-q3", "tyt-fiz-hareket-ve-kuvvet", 3,
                "Bir kişi duvarı 50 N büyüklüğünde kuvvetle itiyor. Duvarın kişiye uyguladığı kuvvet için hangisi doğrudur?",
                ["0 N", "25 N, aynı yönde", "50 N, zıt yönde", "100 N, zıt yönde"], correctIndex: 2,
                "Etki–tepki: duvar kişiye eşit büyüklükte (50 N) ve zıt yönde bir kuvvet uygular."),
        ],
        [
            new("📌", "Newton'un 2. Yasası", "F = m · a", "Net kuvvet, kütle ile ivmenin çarpımına eşittir.",
                Detail: "Bir cisme etki eden net kuvvet, cismin kütlesi ile kazandığı ivmenin çarpımına eşittir. Kuvvet newton (N), kütle kilogram (kg), ivme m/s² birimiyle ölçülür. İvme her zaman net kuvvetle aynı yöndedir. Birden fazla kuvvet varsa önce net kuvvet bulunur (aynı yöndekiler toplanır, zıt yöndekiler çıkarılır), formül sonra uygulanır.",
                Example: "2 kg kütleli bir cisme sağa 14 N, sola 4 N kuvvet etki ediyor. Net kuvvet 14 − 4 = 10 N ve sağa doğrudur; ivme a = F / m = 10 / 2 = 5 m/s², yönü sağa olur."),
            new("💡", "Eylemsizlik", "Net kuvvet sıfırsa cisim durur ya da sabit hızla gider", "Newton'un 1. yasası.",
                Detail: "Cisimler hareket durumlarını korumaya eğilimlidir; buna eylemsizlik denir (Newton’un 1. yasası). Net kuvvet sıfırsa duran cisim durmaya, hareket eden cisim aynı doğrultuda sabit hızla gitmeye devam eder. “Kuvvet yoksa cisim durur” düşüncesi yanlıştır; hareket eden cismi yavaşlatan genellikle sürtünme kuvvetidir. Kütlesi büyük olan cismin eylemsizliği de büyüktür.",
                Example: "Düz yolda sabit hızla giden bir arabaya etki eden net kuvvet sıfırdır: motorun ileri itmesi sürtünme ve hava direncine eşittir. Araba fren yaptığında yolcular öne doğru savrulur, çünkü bedenleri önceki hareketini sürdürmeye çalışır."),
            new("⚡", "Etki–Tepki", "Etki ve tepki kuvvetleri eşit büyüklükte ve zıt yönlüdür", "Farklı cisimlere etki ettikleri için birbirini dengelemez.",
                Detail: "Bir cisim diğerine kuvvet uyguladığında ikinci cisim de birinciye aynı büyüklükte ve zıt yönde kuvvet uygular (Newton’un 3. yasası). Bu iki kuvvet aynı anda oluşur ve her zaman farklı cisimlere etki eder. Farklı cisimlere etki ettikleri için birbirini dengelemez; denge için kuvvetlerin aynı cisme etki etmesi gerekir. Kütleler farklıysa kuvvetler eşit olsa da ivmeler farklı olur.",
                Example: "Masada duran kitabı Dünya aşağı çeker (ağırlık); kitap da Dünya’yı aynı büyüklükte yukarı çeker, etki–tepki çifti budur. Masanın kitabı yukarı itmesi ağırlığın tepkisi değildir; o kuvvet, kitabın masayı aşağı itmesinin tepkisidir."),
        ]),
        new("tyt-kim-atom-ve-periyodik-sistem", "Atom ve Periyodik Sistem", "TYT Kimya", new SeedTest(22, 25, new DueOnDay(3)),
        [
            Question("kim-q1", "tyt-kim-atom-ve-periyodik-sistem", 1,
                "Atom numarası 11 olan nötr bir atomun kaç elektronu vardır?",
                ["11", "22", "12", "10"], correctIndex: 0,
                "Nötr atomda elektron sayısı proton sayısına, yani atom numarasına eşittir: 11."),
            Question("kim-q2", "tyt-kim-atom-ve-periyodik-sistem", 2,
                "Kütle numarası 23, atom numarası 11 olan bir atomun nötron sayısı kaçtır?",
                ["11", "12", "23", "34"], correctIndex: 1,
                "Nötron sayısı = kütle numarası − atom numarası = 23 − 11 = 12."),
            Question("kim-q3", "tyt-kim-atom-ve-periyodik-sistem", 3,
                "Modern periyodik sistemde elementler neye göre sıralanır?",
                ["Artan atom numarasına göre", "Artan kütle numarasına göre", "Artan nötron sayısına göre", "Alfabetik sıraya göre"], correctIndex: 0,
                "Modern periyodik sistem, artan atom numarasına (proton sayısına) göre düzenlenmiştir."),
        ],
        [
            new("📌", "Atom Numarası", "Atom numarası = proton sayısı", "Nötr atomda elektron sayısı da proton sayısına eşittir.",
                Detail: "Atom numarası (Z), çekirdekteki proton sayısıdır ve elementin kimliğini belirler; proton sayısı değişirse element de değişir. Nötr atomda elektron sayısı proton sayısına eşittir. İyonlarda proton sayısı aynı kalır, elektron sayısı değişir. Elektron veren atom pozitif, elektron alan atom negatif yüklü iyon olur.",
                Example: "₁₁Na⁺ iyonunda proton sayısı 11’dir. Bir elektron verdiği için elektron sayısı 11 − 1 = 10 olur. ₈O²⁻ iyonunda ise 8 proton ve 8 + 2 = 10 elektron vardır."),
            new("💡", "Kütle Numarası", "Kütle numarası = proton + nötron", "Nötron sayısı = kütle numarası − atom numarası.",
                Detail: "Kütle numarası (A), çekirdekteki proton ve nötron sayılarının toplamıdır. Elektronların kütlesi çok küçük olduğu için kütle numarasına katılmaz. Nötron sayısı, kütle numarasından atom numarası çıkarılarak bulunur: n = A − Z. Proton sayısı aynı, nötron sayısı farklı atomlara izotop denir.",
                Example: "Kütle numarası 23, atom numarası 11 olan sodyum atomunda nötron sayısı 23 − 11 = 12’dir. ¹²C ve ¹⁴C atomlarının ikisinde de 6 proton vardır; nötron sayıları 6 ve 8 olduğu için izotoptur."),
            new("⚡", "Periyodik Sistem", "Elementler artan atom numarasına göre sıralanır", "Yatay sıralar periyot, dikey sütunlar grup olarak adlandırılır.",
                Detail: "Modern periyodik sistemde elementler artan atom numarasına göre dizilir. Yatay sıralara periyot denir; aynı periyottaki elementlerin elektron katmanı sayısı aynıdır. Dikey sütunlara grup denir; aynı A grubundaki elementlerin değerlik elektronu sayısı aynı olduğundan kimyasal özellikleri benzerdir. Bir elementin yeri, elektronlarının katmanlara dağılımından bulunabilir.",
                Example: "₁₁Na’nın elektron dağılımı 2, 8, 1’dir. Üç katmanı olduğu için 3. periyotta, son katmanında 1 elektron olduğu için 1A grubundadır."),
        ]),
        // Kept as content under its stable Id, but not part of the five-subject demo scenario.
        new("ayt-mat-fonksiyonlar", "Fonksiyonlar", "AYT Matematik", Test: null,
        [
            Question("fonk-q1", "ayt-mat-fonksiyonlar", 1,
                "f(x) = 2x + 1 ve g(x) = x² olduğuna göre, (f ∘ g)(3) kaçtır?",
                ["19", "49", "10", "37"], correctIndex: 0,
                "(f ∘ g)(3) = f(g(3)) = f(9) = 2 · 9 + 1 = 19. 49 ise (g ∘ f)(3) değeridir."),
            Question("fonk-q2", "ayt-mat-fonksiyonlar", 2,
                "f bire bir ve örten bir fonksiyon ve f(2) = 7 olduğuna göre, f⁻¹(7) kaçtır?",
                ["7", "2", "1/7", "−2"], correctIndex: 1,
                "f(a) = b ise f⁻¹(b) = a olur; f(2) = 7 olduğundan f⁻¹(7) = 2."),
            Question("fonk-q3", "ayt-mat-fonksiyonlar", 3,
                "f(x) = x + 1 ve g(x) = 2x olduğuna göre, aşağıdakilerden hangisi doğrudur?",
                ["(f ∘ g)(1) = (g ∘ f)(1)", "(f ∘ g)(1) = 3", "(g ∘ f)(1) = 3", "(f ∘ g)(1) = 4"], correctIndex: 1,
                "(f ∘ g)(1) = f(2) = 3, (g ∘ f)(1) = g(2) = 4; sıra değişince sonuç değişir."),
        ],
        [
            new("📌", "Bileşke", "(f ∘ g)(x) = f(g(x))", "Önce g, sonra f uygulanır.",
                Detail: "Bileşke fonksiyonda içteki fonksiyon önce uygulanır, çıkan sonuç dıştaki fonksiyona girdi olur: (f ∘ g)(x) = f(g(x)). Bileşkenin tanımlı olması için g’nin çıktılarının f’nin tanım kümesinde olması gerekir. Değer sorulduğunda önce iç fonksiyonun değeri bulunur, sonra dış fonksiyona yazılır. Kural sorulduğunda f’nin kuralında x yerine g(x) yazılır.",
                Example: "f(x) = 2x + 1 ve g(x) = x² için (f ∘ g)(3) = f(g(3)) = f(9) = 19 olur. Kural olarak (f ∘ g)(x) = 2x² + 1 bulunur."),
            new("💡", "Ters Fonksiyon", "f(a) = b ise f⁻¹(b) = a", "Ters fonksiyon, girdi ile çıktının yerini değiştirir.",
                Detail: "Bir fonksiyonun tersinin olması için bire bir ve örten olması gerekir. Ters fonksiyon, f’nin yaptığını geri alır: f(a) = b ise f⁻¹(b) = a. Tersi bulmak için y = f(x) yazılır, x yalnız bırakılır ve x ile y yer değiştirilir. Bir fonksiyonla tersinin bileşkesi birim fonksiyondur: (f ∘ f⁻¹)(x) = x.",
                Example: "f(x) = 2x + 1 için y = 2x + 1 yazılır ve x = (y − 1) / 2 bulunur; f⁻¹(x) = (x − 1) / 2 olur. Kontrol: f(2) = 5 ve f⁻¹(5) = (5 − 1) / 2 = 2."),
            new("⚡", "Sık Karıştırılan", "(f ∘ g)(x) ile (g ∘ f)(x) genellikle eşit değildir", "Bileşkede uygulama sırası önemlidir.",
                Detail: "Bileşke işleminde uygulama sırası sonucu değiştirir; bu yüzden işlemin değişme özelliği yoktur. (f ∘ g)(x) önce g’yi, (g ∘ f)(x) önce f’yi uygular. Bir fonksiyon ile tersi gibi bazı özel çiftlerde iki sıra aynı sonucu verebilir, ama bu genel bir kural değildir. Soruda ∘ işaretinin sağındaki fonksiyonun önce uygulandığını unutma.",
                Example: "f(x) = 2x + 1 ve g(x) = x² için (f ∘ g)(x) = 2x² + 1, (g ∘ f)(x) = (2x + 1)² = 4x² + 4x + 1 olur. x = 1 için sonuçlar 3 ve 9’dur; ikisi farklıdır."),
        ]),
    ];

    /// <summary>
    /// Creates the schema and seeds the demo data once. An existing database keeps its student data and test
    /// dates; it only receives demo content it lacks (see <see cref="AddMissingContent"/>). The initial scenario
    /// reaches an existing database only through the explicit demo reset.
    /// </summary>
    public static void Initialize(AppDbContext db, DateTimeOffset seededAtUtc, RetentionModelOptions? options = null)
    {
        db.Database.EnsureCreated();
        EnsureSchemaIsCurrent(db);
        if (db.Students.Any())
        {
            AddMissingContent(db);
            return;
        }

        db.Students.Add(new Student { Id = DemoStudentId, DisplayName = "Demo Öğrenci" });
        db.Topics.AddRange(Topics.Select(CreateTopic));
        db.StudentTopicStates.AddRange(DemoTopicIds.Select(id => new StudentTopicState { StudentId = DemoStudentId, TopicId = id }));
        db.TestResults.AddRange(CreateSeedTestResults(seededAtUtc, options));

        // One SaveChanges runs in a single transaction, so a seed is never left half-written.
        db.SaveChanges();
    }

    /// <summary>Topics of the demo student's scenario, in display order.</summary>
    public static IReadOnlyList<string> DemoTopicIds { get; } = Topics.Where(seed => seed.Test is not null).Select(seed => seed.Id).ToList();

    /// <summary>
    /// Adds the demo topics and quiz questions an existing database lacks, matched by Id, so a database seeded
    /// before them gets the content on the next start. Only content is added: existing topics, questions,
    /// results, sessions and student topic states are never changed, and a second run adds nothing.
    /// </summary>
    private static void AddMissingContent(AppDbContext db)
    {
        var existingTopics = db.Topics.Select(topic => topic.Id).ToHashSet();
        var existingQuestions = db.QuizQuestions.Select(question => question.Id).ToHashSet();
        var missingTopics = Topics.Where(seed => !existingTopics.Contains(seed.Id)).ToList();
        var missingQuestions = Topics
            .Where(seed => existingTopics.Contains(seed.Id))
            .SelectMany(seed => seed.Questions)
            .Where(question => !existingQuestions.Contains(question.Id))
            .ToList();
        if (missingTopics.Count == 0 && missingQuestions.Count == 0) return;

        db.Topics.AddRange(missingTopics.Select(CreateTopic));
        db.QuizQuestions.AddRange(missingQuestions);
        db.SaveChanges();
    }

    private static Topic CreateTopic(SeedTopic seed) => new()
    {
        Id = seed.Id,
        Title = seed.Title,
        Category = seed.Category,
        SortOrder = Array.IndexOf(Topics, seed) + 1,
        Questions = [.. seed.Questions],
    };

    /// <summary>
    /// Micro-review recall points of a topic. Static demo content, served from these definitions
    /// rather than stored in the database.
    /// </summary>
    public static IReadOnlyList<SummaryPoint> GetSummaryPoints(string topicId) =>
        Topics.SingleOrDefault(topic => topic.Id == topicId)?.SummaryPoints ?? [];

    /// <summary>
    /// The demo student's synthetic normal-test results for one reference instant (the first seed or a demo
    /// reset). Each completion time is the target threshold crossing minus h · log2(A / threshold), the same
    /// curve the model evaluates, so the scenario holds for any configured h and threshold.
    /// </summary>
    public static IEnumerable<TestResult> CreateSeedTestResults(DateTimeOffset seededAtUtc, RetentionModelOptions? options = null)
    {
        options ??= new RetentionModelOptions();
        var now = seededAtUtc.ToUniversalTime();
        var today = TimeZoneInfo.ConvertTime(now, DemoTimeZone).Date;
        var todayStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(today, DemoTimeZone));

        return Topics
            .Where(seed => seed.Test is not null)
            .Select(seed =>
            {
                var test = seed.Test!;
                var crossing = test.Target switch
                {
                    DueEarlierToday due => todayStart + (now - todayStart) * due.FractionOfDaySoFar,
                    DueOnDay day => new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                        today.AddDays(day.DaysFromToday).AddHours(FutureReviewHour), DemoTimeZone)),
                    _ => throw new InvalidOperationException($"Unknown review target for '{seed.Id}'."),
                };
                var days = RetentionModel.DaysUntilThreshold((double)test.CorrectCount / test.TotalQuestions, options)
                    ?? throw new InvalidOperationException(
                        $"Demo score {test.CorrectCount}/{test.TotalQuestions} of '{seed.Id}' must be above the review threshold.");

                return new TestResult
                {
                    StudentId = DemoStudentId,
                    TopicId = seed.Id,
                    TestType = TestType.NormalTest,
                    // The exact inverse of the model's CompletedAt.AddDays(days).
                    CompletedAt = crossing.AddDays(-days),
                    CorrectCount = test.CorrectCount,
                    TotalQuestions = test.TotalQuestions,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Columns added after the Phase 6 schema that an existing file can safely receive in place: additive,
    /// with a default that keeps every stored row's meaning. ReviewSessions.PerfectReviewStreak = 0 means
    /// earlier completions never count toward the extended review interval.
    /// </summary>
    private static readonly Dictionary<string, string> AdditiveColumns = new()
    {
        ["ReviewSessions.PerfectReviewStreak"] =
            "ALTER TABLE \"ReviewSessions\" ADD COLUMN \"PerfectReviewStreak\" INTEGER NOT NULL DEFAULT 0",
    };

    /// <summary>
    /// EnsureCreated leaves an existing file as is, so a database from an earlier schema lacks newer
    /// tables or columns. When only additive columns are missing they are added in one transaction and no
    /// row is changed; otherwise startup fails with a clear message instead of on a later request, and the
    /// file is not altered.
    /// </summary>
    private static void EnsureSchemaIsCurrent(AppDbContext db)
    {
        var existing = db.Database
            .SqlQuery<string>($"""
                SELECT m.name || '.' || c.name AS Value
                FROM sqlite_master AS m JOIN pragma_table_info(m.name) AS c
                WHERE m.type = 'table'
                """)
            .ToHashSet();
        var missing = db.Model.GetEntityTypes()
            .SelectMany(entity =>
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
                return entity.GetProperties().Select(property => $"{table.Name}.{property.GetColumnName(table)}");
            })
            .Where(column => !existing.Contains(column))
            .Order()
            .ToList();

        if (missing.Count > 0 && missing.All(AdditiveColumns.ContainsKey))
        {
            using var transaction = db.Database.BeginTransaction();
            foreach (var column in missing) db.Database.ExecuteSqlRaw(AdditiveColumns[column]);
            transaction.Commit();
            return;
        }
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Database '{db.Database.GetDbConnection().DataSource}' was created with an earlier schema " +
                $"and lacks columns: {string.Join(", ", missing)}. Point Database:Path at a new file.");
        }
    }

    private static QuizQuestion Question(
        string id, string topicId, int sortOrder, string text, List<string> options, int correctIndex, string explanation) =>
        new()
        {
            Id = id,
            TopicId = topicId,
            SortOrder = sortOrder,
            Text = text,
            Options = options,
            CorrectIndex = correctIndex,
            Explanation = explanation,
        };
}
