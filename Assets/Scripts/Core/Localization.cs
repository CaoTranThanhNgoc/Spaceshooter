using System.Collections.Generic;
using UnityEngine;

namespace SpaceHawk.Core
{
    public enum Language { English, Vietnamese }

    /// <summary>Small key -> (English, Vietnamese) string table. UI text opts in by carrying a
    /// LocalizedText component (see SpaceHawk.UI) with one of these keys instead of a hardcoded
    /// string; anything without one just keeps whatever literal text it was built with.</summary>
    public static class Localization
    {
        private static readonly Dictionary<string, (string en, string vi)> Strings = new Dictionary<string, (string, string)>
        {
            { "menu.play", ("PLAY GAME", "CHƠI NGAY") },
            { "menu.settings", ("SETTINGS", "CÀI ĐẶT") },

            { "levelselect.missions", ("MISSIONS", "NHIỆM VỤ") },
            { "levelselect.endless", ("ENDLESS", "VÔ TẬN") },

            { "common.crystals_fmt", ("+{0} Crystals!", "+{0} Crystal!") },

            { "missions.title", ("DAILY MISSIONS", "NHIỆM VỤ HẰNG NGÀY") },
            { "missions.daily_login", ("DAILY LOGIN", "ĐĂNG NHẬP HẰNG NGÀY") },
            { "missions.daily_login_sub_fmt", ("DAY {0}/{1} - KEEP YOUR STREAK", "NGÀY {0}/{1} - GIỮ CHUỖI NGÀY") },
            { "missions.all_done", ("COMPLETE ALL MISSIONS", "HOÀN THÀNH TẤT CẢ") },
            { "missions.resets_fmt", ("NEW MISSIONS IN {0}", "NHIỆM VỤ MỚI SAU {0}") },
            { "missions.toast_done_fmt", ("Mission complete: {0}", "Hoàn thành nhiệm vụ: {0}") },
            { "missions.kind_kills", ("DESTROY ENEMIES", "TIÊU DIỆT ĐỊCH") },
            { "missions.kind_kills_fmt", ("DESTROY {0} ENEMIES", "TIÊU DIỆT {0} ĐỊCH") },
            { "missions.kind_levels", ("WIN LEVELS", "THẮNG MÀN") },
            { "missions.kind_levels_fmt", ("WIN {0} LEVELS", "THẮNG {0} MÀN") },
            { "missions.kind_powerups", ("COLLECT POWER-UPS", "NHẶT VẬT PHẨM") },
            { "missions.kind_powerups_fmt", ("COLLECT {0} POWER-UPS", "NHẶT {0} VẬT PHẨM") },
            { "missions.kind_stars", ("PERFECT WINS", "THẮNG HOÀN HẢO") },
            { "missions.kind_stars_fmt", ("WIN {0} LEVELS WITH 3 STARS", "THẮNG {0} MÀN VỚI 3 SAO") },
            { "missions.kind_endless", ("ENDLESS SURVIVOR", "SỐNG SÓT VÔ TẬN") },
            { "missions.kind_endless_fmt", ("REACH WAVE {0} IN ENDLESS MODE", "ĐẠT ĐỢT {0} Ở CHẾ ĐỘ VÔ TẬN") },

            { "score.kills", ("KILLS", "TIÊU DIỆT") },
            { "score.health", ("HEALTH KEPT", "GIỮ MÁU") },
            { "score.speed", ("SPEED", "TỐC ĐỘ") },
            { "score.difficulty", ("DIFFICULTY", "ĐỘ KHÓ") },
            { "score.total", ("SCORE", "ĐIỂM") },
            { "score.best_before", ("PREVIOUS BEST", "KỶ LỤC CŨ") },
            { "score.rating", ("SKILL RATING", "ĐIỂM KỸ NĂNG") },
            { "score.gained", ("RATING GAINED", "ĐIỂM TĂNG THÊM") },

            { "endless.title", ("ENDLESS", "VÔ TẬN") },
            { "endless.objective_fmt", ("SURVIVE AS LONG AS YOU CAN - BEST: WAVE {0}", "SỐNG SÓT CÀNG LÂU CÀNG TỐT - KỶ LỤC: ĐỢT {0}") },
            { "endless.objective_first", ("SURVIVE AS LONG AS YOU CAN", "SỐNG SÓT CÀNG LÂU CÀNG TỐT") },
            { "endless.wave_fmt", ("WAVE {0}", "ĐỢT {0}") },
            { "endless.boss_wave_fmt", ("BOSS - WAVE {0}", "TRÙM - ĐỢT {0}") },
            { "endless.result_fmt", ("Wave {0}  -  Score {1}", "Đợt {0}  -  Điểm {1}") },
            { "endless.reward_fmt", ("+{0} Crystals", "+{0} Crystal") },
            { "endless.new_best", ("NEW BEST!", "KỶ LỤC MỚI!") },
            { "hud.endless_kills", ("KILLS", "HẠ") },

            { "ach.endless.title", ("ENDLESS SURVIVOR", "SỐNG SÓT VÔ TẬN") },
            { "ach.endless.subtitle_fmt", ("REACH WAVE {0} IN ENDLESS MODE", "ĐẠT ĐỢT {0} Ở CHẾ ĐỘ VÔ TẬN") },
            { "levelselect.inventory", ("INVENTORY", "KHO ĐỒ") },
            { "levelselect.achievements", ("ACHIEVEMENTS", "THÀNH TỰU") },
            { "levelselect.leaderboard", ("LEADERBOARD", "BẢNG XẾP HẠNG") },
            { "levelselect.howtoplay", ("HOW TO PLAY", "HƯỚNG DẪN") },

            { "leaderboard.choose_name_title", ("CHOOSE YOUR PILOT NAME", "CHỌN TÊN PHI CÔNG CỦA BẠN") },
            { "leaderboard.choose_name_desc", ("This name will be shown on the Leaderboard for everyone to see.", "Tên này sẽ hiển thị trên Bảng xếp hạng cho mọi người thấy.") },
            { "leaderboard.name_placeholder", ("Enter a name...", "Nhập tên...") },
            { "leaderboard.random", ("RANDOM", "NGẪU NHIÊN") },
            { "leaderboard.confirm", ("CONFIRM", "XÁC NHẬN") },

            { "profile.panel_title", ("PILOT PROFILE", "HỒ SƠ PHI CÔNG") },
            { "profile.section_name", ("1. DISPLAY NAME", "1. TÊN HIỂN THỊ") },
            { "profile.section_stats", ("2. STATS", "2. THỐNG KÊ") },
            { "profile.section_account", ("3. ONLINE ACCOUNT", "3. TÀI KHOẢN ONLINE") },
            { "profile.score_fmt", ("Skill rating: {0}", "Điểm kỹ năng: {0}") },
            { "profile.account_hint_guest", ("Create an account to keep your progress if you switch devices.", "Tạo tài khoản để không mất tiến trình khi đổi thiết bị.") },

            { "authgate.title", ("WELCOME, PILOT", "CHÀO MỪNG PHI CÔNG") },
            { "authgate.desc", ("Sign in to keep your progress across devices, or jump right in as a guest.", "Đăng nhập để lưu tiến trình trên nhiều thiết bị, hoặc chơi ngay với tư cách khách.") },
            { "account.continue_guest", ("PLAY AS GUEST", "CHƠI VỚI TƯ CÁCH KHÁCH") },
            { "authgate.quit", ("QUIT GAME", "THOÁT TRÒ CHƠI") },
            { "menu.quit", ("QUIT GAME", "THOÁT TRÒ CHƠI") },

            { "account.status_guest", ("Playing as Guest", "Đang chơi với tư cách khách") },
            { "account.status_linked_fmt", ("Signed in as: {0}", "Đã đăng nhập: {0}") },
            { "account.register", ("CREATE ACCOUNT", "TẠO TÀI KHOẢN") },
            { "account.signin", ("SIGN IN", "ĐĂNG NHẬP") },
            { "account.signout", ("LOG OUT", "ĐĂNG XUẤT") },
            { "account.delete", ("DELETE ACCOUNT", "XÓA TÀI KHOẢN") },
            { "account.register_title", ("CREATE ACCOUNT", "TẠO TÀI KHOẢN") },
            { "account.register_desc", ("Create an account so you can recover your identity on another device.", "Tạo tài khoản để khôi phục danh tính của bạn trên thiết bị khác.") },
            { "account.signin_title", ("SIGN IN", "ĐĂNG NHẬP") },
            { "account.signin_desc", ("Sign in to restore an existing account.", "Đăng nhập để khôi phục tài khoản đã có.") },
            { "account.username_placeholder", ("Username", "Tên đăng nhập") },
            { "account.password_placeholder", ("Password", "Mật khẩu") },
            { "account.confirm_password_placeholder", ("Confirm password", "Xác nhận mật khẩu") },
            { "account.username_hint", ("3-20 characters: letters, numbers, . _ or -", "3-20 ký tự: chữ, số, dấu . _ hoặc -") },
            { "account.error_invalid_username", ("Username needs 3-20 characters (letters, numbers, . _ or -).", "Tên đăng nhập cần 3-20 ký tự (chữ, số, dấu . _ hoặc -).") },
            { "account.password_hint", ("8-30 characters, with uppercase, lowercase, a number and a symbol.", "8-30 ký tự, có chữ hoa, chữ thường, số và ký hiệu.") },
            { "account.error_weak_password", ("Password needs 8-30 characters with uppercase, lowercase, a number and a symbol (like ! ? # @).", "Mật khẩu cần 8-30 ký tự, gồm chữ hoa, chữ thường, số và ký hiệu (như ! ? # @).") },
            { "account.fill_all_fields", ("Please fill in all fields.", "Vui lòng điền đầy đủ thông tin.") },
            { "account.passwords_dont_match", ("Passwords do not match.", "Mật khẩu không khớp.") },
            { "account.register_success", ("Account created!", "Đã tạo tài khoản!") },
            { "account.signin_success", ("Signed in successfully!", "Đăng nhập thành công!") },
            { "account.signing_out", ("Saving your progress...", "Đang lưu tiến trình...") },
            { "account.show_password", ("SHOW", "HIỆN") },
            { "account.hide_password", ("HIDE", "ẨN") },
            { "account.recent_label", ("Used on this device - tap to fill:", "Đã dùng trên máy này - chạm để điền:") },
            { "account.forgot_password", ("Forgot password?", "Quên mật khẩu?") },
            { "account.forgot_title", ("FORGOT PASSWORD?", "QUÊN MẬT KHẨU?") },
            { "account.change_password", ("CHANGE PASSWORD", "ĐỔI MẬT KHẨU") },
            { "account.change_title", ("CHANGE PASSWORD", "ĐỔI MẬT KHẨU") },
            { "account.change_desc", ("Enter your current password, then choose a new one.", "Nhập mật khẩu hiện tại, rồi chọn mật khẩu mới.") },
            { "account.current_password_placeholder", ("Current password", "Mật khẩu hiện tại") },
            { "account.new_password_placeholder", ("New password", "Mật khẩu mới") },
            { "account.confirm_new_password_placeholder", ("Confirm new password", "Xác nhận mật khẩu mới") },
            { "account.change_success", ("Password changed.", "Đã đổi mật khẩu.") },
            { "account.same_password", ("The new password must be different from the current one.", "Mật khẩu mới phải khác mật khẩu hiện tại.") },
            { "account.contact_placeholder", ("Email", "Email") },
            { "account.contact_hint", ("Only used to recover your password. We will send a code to check it is yours.", "Chỉ dùng để lấy lại mật khẩu. Chúng tôi sẽ gửi mã để xác nhận đây là của bạn.") },
            { "account.error_invalid_contact", ("Enter a valid email address.", "Hãy nhập địa chỉ email hợp lệ.") },
            { "account.error_too_soon_fmt", ("Please wait {0}s before asking for another code.", "Vui lòng đợi {0} giây rồi yêu cầu mã mới.") },
            { "account.error_send_failed", ("The code could not be sent. Please try again.", "Không gửi được mã. Vui lòng thử lại.") },
            { "account.error_invalid_code", ("That code is not right.", "Mã không đúng.") },
            { "account.error_code_expired", ("That code has expired. Ask for a new one.", "Mã đã hết hạn. Hãy yêu cầu mã mới.") },
            { "account.error_too_many_attempts", ("Too many wrong tries. Ask for a new code.", "Nhập sai quá nhiều lần. Hãy yêu cầu mã mới.") },
            { "account.error_server_unavailable", ("The account server isn't available right now. Please try again later.", "Máy chủ tài khoản chưa sẵn sàng. Vui lòng thử lại sau.") },
            { "account.register_success_nocontact", ("Account created, but the recovery email could not be saved - add it again in your Profile.", "Đã tạo tài khoản, nhưng chưa lưu được email khôi phục - hãy thêm lại trong Hồ sơ.") },
            { "account.forgot_step1_desc", ("Enter your username and the email saved on your account. We will send you a code.", "Nhập tên đăng nhập và email đã lưu cho tài khoản. Chúng tôi sẽ gửi mã cho bạn.") },
            { "account.forgot_step2_desc_fmt", ("Enter the 6-digit code sent to {0}, then choose a new password.", "Nhập mã 6 số đã gửi tới {0}, rồi chọn mật khẩu mới.") },
            { "account.forgot_no_contact", ("Never saved an email on this account? Then it cannot be reset - create a new account instead.", "Chưa lưu email cho tài khoản này? Khi đó không thể đặt lại - hãy tạo tài khoản mới.") },
            { "account.forgot_send", ("SEND CODE", "GỬI MÃ") },
            { "account.forgot_resend", ("SEND A NEW CODE", "GỬI MÃ MỚI") },
            { "account.forgot_reset", ("RESET PASSWORD", "ĐẶT LẠI MẬT KHẨU") },
            { "account.code_placeholder", ("6-digit code", "Mã 6 số") },
            { "account.forgot_sent", ("If these details match an account, a code is on its way.", "Nếu thông tin khớp với một tài khoản, mã đang được gửi đi.") },
            { "account.forgot_done", ("Password changed - you can sign in now.", "Đã đổi mật khẩu - bạn có thể đăng nhập.") },
            { "account.contact_title", ("RECOVERY EMAIL", "EMAIL KHÔI PHỤC") },
            { "account.contact_desc", ("Add an email so a forgotten password can be reset. We will send a code to confirm it.", "Thêm email để có thể đặt lại mật khẩu khi quên. Chúng tôi sẽ gửi mã để xác nhận.") },
            { "account.contact_current_fmt", ("Current: {0}", "Hiện tại: {0}") },
            { "account.contact_save", ("SAVE", "LƯU") },
            { "account.contact_saved", ("Recovery email saved.", "Đã lưu email khôi phục.") },
            { "account.contact_add", ("ADD", "THÊM") },
            { "account.contact_change", ("CHANGE", "ĐỔI") },
            { "profile.recovery_fmt", ("Recovery: {0}", "Khôi phục: {0}") },
            { "profile.recovery_none", ("No recovery email - a forgotten password could not be reset", "Chưa có email khôi phục - quên mật khẩu sẽ không lấy lại được") },
            { "account.verify_title", ("VERIFY EMAIL", "XÁC MINH EMAIL") },
            { "account.verify_desc_fmt", ("We sent a 6-digit code to {0}. Enter it to confirm this is yours.", "Chúng tôi đã gửi mã 6 số tới {0}. Nhập mã để xác nhận đây là của bạn.") },
            { "account.verify_button", ("VERIFY", "XÁC MINH") },
            { "account.verify_sent_fmt", ("Code sent to {0}.", "Đã gửi mã tới {0}.") },
            { "account.contact_moved", ("This email belonged to another account. It is now linked to this one.", "Email này trước đó thuộc tài khoản khác. Giờ nó đã chuyển sang tài khoản này.") },
            { "account.error_not_verified", ("This email has not been verified yet. Please verify it first.", "Email này chưa được xác minh. Hãy xác minh trước.") },
            { "account.error_rate_limited", ("Too many codes were sent to that email. Please try again in an hour.", "Đã gửi quá nhiều mã tới email này. Vui lòng thử lại sau một giờ.") },
            { "account.signout_success", ("Logged out - back to your guest progress.", "Đã đăng xuất - quay về tiến trình khách.") },
            { "account.delete_confirm_title", ("DELETE ACCOUNT?", "XÓA TÀI KHOẢN?") },
            { "account.delete_confirm_desc", ("This permanently deletes your online account, your saved progress and your place on the Leaderboard, and cannot be undone. This device goes back to your guest progress.", "Thao tác này sẽ xóa vĩnh viễn tài khoản online, tiến trình đã lưu và thứ hạng trên Bảng xếp hạng của bạn, không thể hoàn tác. Thiết bị sẽ quay về tiến trình khách của bạn.") },
            { "account.delete_success", ("Account deleted.", "Đã xóa tài khoản.") },
            { "account.delete_failed", ("Could not delete account - check your connection and try again.", "Không thể xóa tài khoản - kiểm tra kết nối mạng và thử lại.") },

            { "account.error_not_connected", ("No internet connection. Please check your network.", "Không có kết nối mạng. Vui lòng kiểm tra internet.") },
            { "account.error_invalid_params", ("Username or password is not valid.", "Tên đăng nhập hoặc mật khẩu không hợp lệ.") },
            { "account.error_already_linked", ("This account is already linked to another player.", "Tài khoản này đã được liên kết với người chơi khác.") },
            { "account.error_provider_disabled", ("Username/password sign-in isn't enabled for this project yet (check Unity Dashboard > Authentication).", "Đăng nhập bằng tài khoản chưa được bật cho dự án này (kiểm tra Unity Dashboard > Authentication).") },
            { "account.error_banned", ("This account has been banned.", "Tài khoản này đã bị khóa.") },
            { "account.error_username_taken", ("This username is already taken. Please choose a different one.", "Tên đăng nhập này đã có người dùng. Vui lòng chọn tên khác.") },
            { "account.error_wrong_credentials", ("Incorrect username or password.", "Sai tên đăng nhập hoặc mật khẩu.") },
            { "account.error_service_unavailable", ("Account service is temporarily unavailable. Please try again later.", "Dịch vụ tài khoản hiện chưa khả dụng. Vui lòng thử lại sau.") },
            { "account.error_generic", ("Something went wrong. Please try again.", "Đã xảy ra lỗi. Vui lòng thử lại.") },

            { "confirm.yes", ("YES", "CÓ") },
            { "confirm.no", ("NO", "KHÔNG") },
            { "levelselect.not_enough_energy", ("Not enough energy!", "Không đủ năng lượng!") },
            { "levelselect.guest_level_locked_fmt", ("Sign in to unlock levels past {0}.", "Đăng nhập để mở khóa các màn sau level {0}.") },
            { "levelselect.guest_leaderboard_locked", ("Sign in to view the Leaderboard.", "Đăng nhập để xem Bảng xếp hạng.") },

            { "howtoplay.title", ("HOW TO PLAY", "HƯỚNG DẪN CHƠI") },
            { "howtoplay.controls_title", ("DRAG TO MOVE", "KÉO ĐỂ DI CHUYỂN") },
            { "howtoplay.controls_desc", ("Drag anywhere on screen to move your ship. It fires automatically.", "Kéo ở bất kỳ đâu trên màn hình để di chuyển tàu. Tàu tự động bắn.") },
            { "howtoplay.objective_title", ("DESTROY ENOUGH ENEMIES", "TIÊU DIỆT ĐỦ SỐ ĐỊCH") },
            { "howtoplay.objective_desc", ("Destroy at least the required number of enemies to clear a level. Letting too many escape ends in defeat.", "Tiêu diệt ít nhất số địch yêu cầu để qua màn. Để quá nhiều địch thoát sẽ dẫn đến thua cuộc.") },
            { "howtoplay.powerups_title", ("COLLECT POWER-UPS", "THU THẬP VẬT PHẨM") },
            { "howtoplay.powerups_desc", ("Destroyed enemies may drop power-ups - shields, rapid fire, bombs, and more.", "Địch bị tiêu diệt có thể rơi ra vật phẩm hỗ trợ - khiên chắn, bắn nhanh, bom và nhiều hơn nữa.") },
            { "howtoplay.boss_title", ("DEFEAT THE BOSS", "HẠ GỤC TRÙM CUỐI") },
            { "howtoplay.boss_desc", ("Some levels end in a boss fight. The boss won't leave until it's destroyed - hold your ground!", "Một số màn kết thúc bằng trận đấu trùm. Trùm sẽ không rời đi cho đến khi bị tiêu diệt - giữ vững vị trí!") },
            { "howtoplay.energy_title", ("ENERGY", "NĂNG LƯỢNG") },
            { "howtoplay.energy_desc", ("Each level costs Energy to play. It regenerates over time, so come back later if you run out.", "Mỗi màn chơi tốn Năng lượng. Năng lượng tự hồi theo thời gian, quay lại sau nếu bạn dùng hết.") },
            { "howtoplay.upgrade_title", ("UPGRADE YOUR SHIP", "NÂNG CẤP TÀU") },
            { "howtoplay.upgrade_desc", ("Earn Crystals from levels and achievements to upgrade your ship or unlock new hulls in the Inventory.", "Kiếm Crystal từ các màn chơi và thành tựu để nâng cấp tàu hoặc mở khóa tàu mới trong Kho đồ.") },

            { "settings.title", ("SETTINGS", "CÀI ĐẶT") },
            { "settings.resolution_label", ("DROP DOWN", "ĐỘ PHÂN GIẢI") },
            { "settings.volume", ("MUSIC / SFX VOLUME", "ÂM LƯỢNG NHẠC / HIỆU ỨNG") },
            { "settings.sfx", ("SOUND EFFECTS", "HIỆU ỨNG ÂM THANH") },
            { "settings.on", ("ON", "BẬT") },
            { "settings.off", ("OFF", "TẮT") },
            { "settings.display", ("DISPLAY", "HIỂN THỊ") },
            { "settings.fullscreen", ("FULL SCREEN", "TOÀN MÀN HÌNH") },
            { "settings.language", ("LANGUAGE", "NGÔN NGỮ") },
            { "settings.language_en", ("ENGLISH", "ENGLISH") },
            { "settings.language_vi", ("TIENG VIET", "TIẾNG VIỆT") },

            { "pause.title", ("PAUSED", "TẠM DỪNG") },
            { "pause.resume", ("RESUME", "TIẾP TỤC") },
            { "pause.restart", ("RESTART", "CHƠI LẠI") },
            { "pause.settings", ("SETTINGS", "CÀI ĐẶT") },
            { "pause.menu", ("MENU", "MENU") },

            { "gameover.title", ("GAME OVER", "THUA CUỘC") },
            { "gameover.retry", ("RETRY", "THỬ LẠI") },
            { "gameover.menu", ("MENU", "MENU") },
            { "gameover.reason_defeated", ("Your ship was destroyed", "Tàu của bạn đã bị phá hủy") },
            { "gameover.reason_not_enough_kills", ("Not enough enemies destroyed!", "Chưa tiêu diệt đủ số địch!") },
            { "gameover.revive_fmt", ("REVIVE ({0} CRYSTAL)", "HỒI SINH ({0} CRYSTAL)") },

            { "victory.title", ("VICTORY", "CHIẾN THẮNG") },
            { "victory.next_level", ("NEXT LEVEL", "MÀN TIẾP THEO") },
            { "victory.level_select", ("LEVEL SELECT", "CHỌN MÀN") },
            { "victory.menu", ("MENU", "MENU") },

            { "hud.level", ("LEVEL", "MÀN") },
            { "hud.score", ("SCORE", "ĐIỂM") },

            { "levelintro.objective_fmt", ("DESTROY AT LEAST {0} ENEMIES TO WIN", "TIÊU DIỆT ÍT NHẤT {0} ĐỊCH ĐỂ THẮNG") },

            { "tutorial.drag_to_move", ("DRAG TO MOVE YOUR SHIP", "KÉO ĐỂ DI CHUYỂN TÀU") },


            { "energy.title", ("OUT OF ENERGY", "HẾT NĂNG LƯỢNG") },
            { "energy.message", ("Energy comes back 1 point every 5 minutes. Refill now to keep playing.", "Năng lượng hồi 1 điểm mỗi 5 phút. Nạp ngay để tiếp tục chơi.") },
            { "energy.refill_fmt", ("REFILL ALL: {0} CRYSTAL", "NẠP ĐẦY: {0} CRYSTAL") },
            { "energy.close", ("CLOSE", "ĐÓNG") },
            { "energy.refilled", ("Energy refilled!", "Đã nạp đầy năng lượng!") },
            { "energy.already_full", ("Your energy is already full.", "Năng lượng của bạn đã đầy.") },
            { "common.not_enough_crystals", ("Not enough Crystals.", "Không đủ Crystal.") },

            { "inventory.title", ("INVENTORY", "KHO ĐỒ") },
            { "inventory.hp", ("HEALTH POINTS", "MÁU") },
            { "inventory.dmg", ("DAMAGE POINTS", "SÁT THƯƠNG") },
            { "inventory.upgrade", ("UPGRADE", "NÂNG CẤP") },
            { "inventory.max_level", ("MAX LEVEL", "CẤP TỐI ĐA") },
            { "inventory.ship_unlocked", ("Ship unlocked!", "Đã mở khóa tàu!") },
            { "inventory.not_enough_crystals_fmt", ("Need {0} Crystal to unlock!", "Cần {0} Crystal để mở khóa!") },
            { "inventory.outfits", ("SHIPS", "TÀU") },
            { "inventory.level", ("LEVEL", "CẤP") },
            { "inventory.hull_0", ("HAWK", "HAWK") },
            { "inventory.hull_1", ("VIPER", "VIPER") },
            { "inventory.hull_2", ("PHANTOM", "PHANTOM") },
            { "inventory.role_0", ("INTERCEPTOR - fast and handy", "TIÊM KÍCH - nhanh và linh hoạt") },
            { "inventory.role_1", ("BULWARK - built to endure", "PHÁO ĐÀI - bền bỉ") },
            { "inventory.role_2", ("STRIKER - hits like a truck", "SÁT THỦ - sát thương lớn") },
            { "inventory.stat_rate", ("FIRE RATE", "TỐC ĐỘ BẮN") },
            { "inventory.perks", ("SPECIAL ABILITIES", "ĐẶC TÍNH") },
            { "inventory.perk_none", ("Balanced all-rounder - no special abilities", "Cân bằng - không có đặc tính riêng") },
            { "inventory.perk_twin", ("Twin cannons - two shots at once", "Súng đôi - bắn hai viên cùng lúc") },
            { "inventory.perk_triple", ("Triple cannons - a three-shot fan", "Súng ba - bắn xòe ba viên") },
            { "inventory.perk_pierce_fmt", ("Piercing shots - through {0} extra enemies", "Đạn xuyên - xuyên thêm {0} địch") },
            { "inventory.perk_crit_fmt", ("Critical hits - {0}% chance of double damage", "Chí mạng - {0}% gây sát thương gấp đôi") },
            { "inventory.perk_armor_fmt", ("Armor plating - {0}% less damage taken", "Giáp dày - nhận ít hơn {0}% sát thương") },
            { "inventory.perk_regen", ("Nano-repair - heals when left alone", "Tự sửa chữa - hồi máu khi không bị bắn") },
            { "inventory.perk_barrier_fmt", ("Opens every level behind a {0}s barrier", "Mở màn với khiên {0} giây") },
            { "inventory.perk_second_wind", ("Second wind - survives one fatal hit per level", "Hồi sinh - sống sót một đòn chí tử mỗi màn") },
            { "inventory.perk_magnet", ("Tractor beam - pulls in nearby pickups", "Hút vật phẩm ở gần") },
            { "inventory.perk_buff_fmt", ("Power-ups last {0}% longer", "Vật phẩm kéo dài thêm {0}%") },
            { "inventory.req_ship_fmt", ("OWN {0} FIRST", "CẦN CÓ {0} TRƯỚC") },
            { "inventory.req_level_fmt", ("CLEAR LEVEL {0}", "QUA MÀN {0}") },
            { "ship.second_wind_used", ("Second wind! The ship fights on", "Hồi sinh! Tàu tiếp tục chiến đấu") },
            { "inventory.equip", ("EQUIP", "TRANG BỊ") },
            { "inventory.equipped", ("EQUIPPED", "ĐANG DÙNG") },
            { "inventory.owned", ("OWNED", "ĐÃ SỞ HỮU") },
            { "inventory.unlock_fmt", ("UNLOCK - {0}", "MỞ KHÓA - {0}") },
            { "inventory.equipped_toast_fmt", ("{0} equipped", "Đã trang bị {0}") },

            { "achievements.title", ("ACHIEVEMENTS", "THÀNH TỰU") },
            { "achievements.in_progress", ("IN PROGRESS", "ĐANG THỰC HIỆN") },
            { "achievements.claim", ("CLAIM", "NHẬN") },
            { "achievements.complete", ("COMPLETE", "HOÀN THÀNH") },
            { "achievements.unlocked_fmt", ("Achievement Unlocked: {0}!", "Đạt Thành Tựu: {0}!") },

            { "ach.ship_level.title", ("UPGRADE SHIP", "NÂNG CẤP TÀU") },
            { "ach.ship_level.subtitle_fmt", ("REACH SHIP LEVEL {0}", "ĐẠT CẤP TÀU {0}") },
            { "ach.ship_level.max_title", ("MAX UPGRADE", "NÂNG CẤP TỐI ĐA") },
            { "ach.ship_level.max_subtitle", ("REACH MAX SHIP LEVEL", "ĐẠT CẤP TÀU TỐI ĐA") },
            { "ach.stars.title", ("PERFECT CLEAR", "HOÀN HẢO") },
            { "ach.stars.subtitle_fmt", ("EARN {0} STARS ON A LEVEL", "ĐẠT {0} SAO TRONG 1 MÀN") },
            { "ach.kills.title", ("DESTROY ENEMIES", "TIÊU DIỆT ĐỊCH") },
            { "ach.kills.subtitle_fmt", ("DESTROY {0} ENEMIES", "TIÊU DIỆT {0} ĐỊCH") },
            { "ach.levels.title", ("LEVEL CLEARER", "NGƯỜI CHINH PHỤC MÀN") },
            { "ach.levels.subtitle_fmt", ("CLEAR {0} LEVELS", "QUA {0} MÀN") },
            { "ach.crystals.title", ("CRYSTAL COLLECTOR", "NHÀ SƯU TẦM CRYSTAL") },
            { "ach.crystals.subtitle_fmt", ("EARN {0} CRYSTALS TOTAL", "KIẾM TỔNG {0} CRYSTAL") },
        };

        private static Language? _current;

        /// <summary>Every key of the table (the tests use it to check texts in both languages).</summary>
        public static IEnumerable<string> Keys => Strings.Keys;

        public static Language Current
        {
            get
            {
                if (_current == null) _current = (Language)Mathf.Clamp(SaveManager.Data.language, 0, 1);
                return _current.Value;
            }
        }

        public static event System.Action LanguageChanged;

        public static string Get(string key)
        {
            if (Strings.TryGetValue(key, out (string en, string vi) pair))
                return Current == Language.Vietnamese ? pair.vi : pair.en;
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public static void SetLanguage(Language lang)
        {
            if (_current == lang) return;
            _current = lang;
            SaveManager.SetLanguage((int)lang);
            LanguageChanged?.Invoke();
        }

        public static void CycleLanguage()
        {
            SetLanguage(Current == Language.English ? Language.Vietnamese : Language.English);
        }
    }
}
