# Path to Dragon (P2D)

> **"มังกรคือมรดก ผู้ใช้คือผู้สืบทอด"**

เกม RPG เลี้ยงและพัฒนามังกรบนมือถือ (จอแนวตั้ง) พัฒนาด้วย Unity
ผู้เล่นเลี้ยงมังกรหนึ่งตัวตั้งแต่ฟักจากไข่จนกลายเป็นมังกรโบราณ กำหนดทิศทางการเติบโตผ่านอุปกรณ์ที่อัปเกรดได้
สวมบทเป็น **ผู้ใช้มังกร (Rider)** ที่สลับตัวได้ตลอด และบุก **ดันเจี้ยนรายสัปดาห์** เพื่อหาทรัพยากรอัปเกรด

## เอกสารออกแบบ

| เอกสาร | เนื้อหา |
|---|---|
| [docs/GDD.md](docs/GDD.md) | Game Design Document หลัก: Core loop, มังกร, อุปกรณ์ & Growth Path, ผู้ใช้มังกร & ระดับดาว, ดันเจี้ยนรายสัปดาห์, การต่อสู้, เศรษฐกิจ, Roadmap |
| [docs/Balance.md](docs/Balance.md) | สูตรคำนวณ ตารางค่าพลัง ค่าอัปเกรด อัตราดรอป และจังหวะความคืบหน้า |
| [docs/UI-Portrait.md](docs/UI-Portrait.md) | หลักการ UI แนวตั้ง, การจัดโซนหน้าจอ, Wireframe ทุกหน้าหลัก, ท่าทางสัมผัส, Accessibility |
| [docs/balance-report.md](docs/balance-report.md) | ผลจำลองสมดุลล่าสุด: Build แต่ละ Path ไต่หอคอยได้ถึงชั้นไหน ชนะบอสกี่ % และการปรับที่ทำตามผล |
| [docs/Unity-Tech.md](docs/Unity-Tech.md) | Tech stack, โครงสร้างโปรเจกต์, สถาปัตยกรรม, Data model + ตัวอย่างโค้ด C#, Safe Area/Canvas Scaler, Backend, ภาษาไทย |

## เริ่มพัฒนา (Unity)

- เปิดโฟลเดอร์ repo นี้ด้วย **Unity 6000.3.27f1** (ผ่าน Unity Hub → Add project from disk)
- Player Settings ถูกตั้งเป็นแนวตั้งอัตโนมัติ (เมนู **P2D → Apply Portrait Player Settings** ใช้สั่งซ้ำได้)
- รันเทสต์: **Window → General → Test Runner → EditMode / PlayMode → Run All**
- ลองเล่นเกม greybox: เปิด `Assets/_Project/Scenes/Main.unity` ตั้ง Game view เป็นแนวตั้ง (เช่น 1080×1920) แล้วกด Play — แท็บ NEST / DRAGON / RIDERS / SPIRE / SUMMON ใช้เซฟจริง, ปุ่ม CHALLENGE ในแท็บ SPIRE พาไปฉากต่อสู้แล้วกลับมารับรางวัล · ฉากต่อสู้เปิดเดี่ยว ๆ ได้ที่ `GreyboxBattle.unity` (โหมดทดลอง)
- ในฉากต่อสู้ — แตะศัตรูเพื่อเลือกเป้า, ปัดซ้าย/ขวาหรือกด DODGE เพื่อหลบ, ปุ่ม BUILD สลับ Build ตัวอย่าง (คีย์บอร์ด: 1/2/3 สกิล, Q สกิลบัญชาการ, Space หลบ)

| โฟลเดอร์ | เนื้อหา |
|---|---|
| `Assets/_Project/Scripts/Domain` | กฎเกม C# ล้วน (ไม่พึ่ง UnityEngine): ค่าพลังมังกร, อุปกรณ์/Affinity, ผู้ใช้/ดาว/บัพนักล่า, สูตรต่อสู้, หอคอยรายสัปดาห์, ถ้ำล่าเกล็ด, อัญเชิญ |
| `Assets/_Project/Scripts/Domain/Battle` | ระบบต่อสู้แบบ deterministic (30 tick/วินาที, seed เดียวกัน + input log เดียวกัน = ผลเดียวกัน) ใช้ได้ทั้งเล่นจริง กวาดชั้น และให้เซิร์ฟเวอร์ replay ตรวจ |
| `Assets/_Project/Scripts/Domain/Save` | ข้อมูลเซฟ `PlayerSave`, เซฟพร้อม checksum + backup, migration ข้ามเวอร์ชัน, ความคืบหน้าระหว่างออฟไลน์ (พลังล่า, คบเพลิง, รีเซ็ตรายสัปดาห์) |
| `Assets/_Project/Scripts/Services` | ส่วนเชื่อม Unity: `SaveManager` (เซฟอัตโนมัติ/ตอนพับแอป), `FileSaveStorage` (เขียนไฟล์แบบ atomic), `JsonUtilitySaveSerializer`, `ServerClock` |
| `Assets/_Project/Scripts/Data` | ScriptableObject สำหรับกรอกข้อมูลในเอดิเตอร์ (เช่น `RiderDefinition`) |
| `Assets/_Project/Scripts/Presentation` | UI แนวตั้ง: `SafeAreaFitter`, `PortraitCanvasScaler` · `BattleRunner` เดินการต่อสู้แบบเรียลไทม์ (×1/×2/×3) และส่ง event ให้ฉาก |
| `Assets/_Project/Scripts/Editor` | เครื่องมือเอดิเตอร์: ตั้งค่าจอแนวตั้ง, **P2D → Balance Simulator** (จำลองสมดุลหอคอย) |
| `Assets/_Project/Tests/EditMode` | NUnit tests ที่ตรวจตัวเลขกับ `docs/Balance.md` |

## ระบบหลักโดยย่อ

- **มังกร** — 5 ระยะวิวัฒนาการ (ลูกมังกร → มังกรโบราณ) แต่ละระยะปลดล็อกช่องอุปกรณ์และช่องสกิลเพิ่ม
- **อุปกรณ์ 6 ช่อง** — ทุกชิ้นมี Growth Path (เพลิงโทสะ / ศิลาปราการ / วายุ / มนตรา) ค่า Affinity รวมกันปลดล็อก Trait และสกิล ตีบวก +15 ได้ และเปลี่ยน Path ได้โดยไม่เสียระดับตีบวก
- **ผู้ใช้มังกร** — ระดับ R / SR / SSR / UR ระดับสูงให้บัพมากกว่า อัปดาว 1★–5★ ด้วยตัวซ้ำ บัพเพิ่มตามดาว (สูงสุด ×2.2)
- **หอคอยวิถีมังกร (ดันเจี้ยนรายสัปดาห์)** — 30 ชั้น + ชั้นอนันต์ เปลี่ยนธีม บอส และ Affix ทุกวันจันทร์ เป็นแหล่งเกล็ดโบราณสำหรับวิวัฒนาการ
- **UI แนวตั้ง** — เล่นมือเดียว ปุ่มหลักอยู่ใน Thumb Zone รองรับมือถือจอยาวและแท็บเล็ต
