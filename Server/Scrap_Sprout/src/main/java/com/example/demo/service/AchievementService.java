package com.example.demo.service;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import java.util.*;

@Service
public class AchievementService {

    private final JdbcTemplate jdbcTemplate;

    public AchievementService(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }

    // 1. 업적 목록 조회 — DB 컬럼명을 유니티 AchievementData 필드명으로 매핑해서 반환
    //    실제 ACHIEVEMENT 테이블 컬럼: ACHIEVEMENT_ID, PLAYER_ID, DESIGNATION, DETAIL, ACHIEVED_AT
    //    (진행도/잠금 컬럼이 없음 = 이 테이블에 행이 있으면 '이미 획득한 업적')
    public List<Map<String, Object>> getAchievements(String playerId) {
        String sql = "SELECT * FROM ACHIEVEMENT WHERE PLAYER_ID = ?";
        List<Map<String, Object>> rows = jdbcTemplate.queryForList(sql, playerId);

        List<Map<String, Object>> result = new ArrayList<>();
        for (Map<String, Object> row : rows) {
            Map<String, Object> a = new HashMap<>();
            a.put("achievementType", asStr(row.get("ACHIEVEMENT_ID")));
            a.put("achievementName", asStr(row.get("DESIGNATION")));
            a.put("designation",     asStr(row.get("DESIGNATION")));
            a.put("detail",          asStr(row.get("DETAIL")));
            // 행이 존재 = 달성 완료 (ACHIEVED_AT 기록됨)
            a.put("isCompleted",     true);
            a.put("currentProgress", 1);
            a.put("targetValue",     1);
            a.put("progressPercent", 100.0);
            a.put("rewardGold",      0);
            a.put("rewardExp",       0);
            result.add(a);
        }
        return result;
    }

    private static String asStr(Object o) { return o == null ? "" : o.toString(); }

    // 2. 업적 진행도 업데이트 및 달성 체크
    @Transactional
    public Map<String, Object> updateAchievementProgress(String playerId, String type, int amount) {
        Map<String, Object> result = new HashMap<>();
        
        // 1) 진행도 업데이트 (ON DUPLICATE KEY를 쓰려면 ACHIEVEMENT_INDEX와 PLAYER_ID가 복합키여야 합니다)
        String updateSql = "UPDATE ACHIEVEMENT SET PROGRESS = PROGRESS + ? " +
                           "WHERE PLAYER_ID = ? AND ACHIEVEMENT_INDEX = ? AND IS_UNLOCKED = 0";
        int rows = jdbcTemplate.update(updateSql, amount, playerId, type);

        // 2) 현재 상태 조회 및 달성 체크 (예: 쓰레기 10개 수거가 목표라면)
        String selectSql = "SELECT * FROM ACHIEVEMENT WHERE PLAYER_ID = ? AND ACHIEVEMENT_INDEX = ?";
        Map<String, Object> currentStatus = jdbcTemplate.queryForMap(selectSql, playerId, type);
        
        int progress = (int) currentStatus.get("PROGRESS");
        int isUnlocked = (int) currentStatus.get("IS_UNLOCKED");
        int goal = 10; // 테스트용 목표치 (실제로는 별도 마스터 테이블에 두는 것이 좋습니다)

        if (isUnlocked == 0 && progress >= goal) {
            // 3) 달성 처리
            jdbcTemplate.update("UPDATE ACHIEVEMENT SET IS_UNLOCKED = 1 WHERE PLAYER_ID = ? AND ACHIEVEMENT_INDEX = ?", playerId, type);
            
            // 4) 보상 지급 (PLAYER 테이블의 GOLD 업데이트)
            int rewardGold = 10;
            jdbcTemplate.update("UPDATE PLAYER SET GOLD = GOLD + ? WHERE PLAYER_ID = ?", rewardGold, playerId);

            // 성공 응답 구성
            Map<String, Object> unlocked = new HashMap<>();
            unlocked.put("achievementType", type);
            unlocked.put("achievementName", "첫걸음"); // 예시 이름
            unlocked.put("designation", currentStatus.get("DESIGNATION"));
            unlocked.put("detail", currentStatus.get("DETAIL"));
            unlocked.put("rewardGold", rewardGold);

            result.put("success", true);
            result.put("unlockedAchievement", unlocked);
            result.put("totalRewardGold", rewardGold);
            result.put("totalRewardExp", 5);
        } else {
            result.put("success", true);
            result.put("unlockedAchievement", null);
        }

        return result;
    }
}