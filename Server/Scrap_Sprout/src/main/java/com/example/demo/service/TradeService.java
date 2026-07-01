package com.example.demo.service;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.stereotype.Service;

import java.util.List;
import java.util.Map;

@Service
public class TradeService {

    private final JdbcTemplate jdbcTemplate;

    public TradeService(JdbcTemplate jdbcTemplate) {
        this.jdbcTemplate = jdbcTemplate;
    }
    
    // 1. 아이디 중복 확인 (결과가 0이어야 사용 가능)
    public boolean checkIdDuplicate(String username) {
        String sql = "SELECT COUNT(*) FROM `PLAYER` WHERE PLAYER_ID = ?";
        Integer count = jdbcTemplate.queryForObject(sql, Integer.class, username);
        return count != null && count > 0; // true면 중복됨, false면 사용 가능
    }

    // 2. 회원가입 실행 (PLAYER 테이블에 INSERT)
    public boolean registerUser(String username, String password, String nickname, String email) {
        try {
            // PLAYER_ID, PLAYER_PW, PLAYER_NAME(또는 닉네임 컬럼), EMAIL, 기본 골드(0) 등 설정
            String sql = "INSERT INTO `PLAYER` (PLAYER_ID, PLAYER_PW, NICKNAME, EMAIL, gold) VALUES (?, ?, ?, ?, 0)";
            int rows = jdbcTemplate.update(sql, username, password, nickname, email);
            return rows > 0; // 성공 시 true
        } catch (Exception e) {
            System.out.println("[Service] 회원가입 실패: " + e.getMessage());
            return false;
        }
    }
    
    // TradeService.java 내부에 추가
    public Map<String, Object> getUserByUsername(String username, String password) {
        try {
            System.out.println("   [Service] DB 조회 중: " + username);
            System.out.println("   [Service] DB 조회 중: " + password);
            String sql = "SELECT * FROM PLAYER WHERE PLAYER_ID = ? AND PLAYER_PW = ?";
            
            // ✅ query로 변경 — 결과 없으면 null 반환
            List<Map<String, Object>> results = jdbcTemplate.queryForList(sql, username, password);
            return results.isEmpty() ? null : results.get(0);
            
        } catch (Exception e) {
            System.out.println("[Service] 유저를 찾을 수 없음: " + e.getMessage());
            return null;
        }
    }
    // 플레이어 정보 가져오기
    public Map<String, Object> getPlayer(String username) {
        String sql = "SELECT * FROM `PLAYER` WHERE PLAYER_ID = ?";
        return jdbcTemplate.queryForMap(sql, username);
    }
    
    public int updateGold(String playerId, int amount) {
        System.out.println(playerId + " : " + amount);
        // 1. 골드 업데이트 (기존 코드)
        String updateSql = "UPDATE `PLAYER` SET gold = gold + ? WHERE PLAYER_ID = ?";
        jdbcTemplate.update(updateSql, amount, playerId);
        
        // 2. 업데이트된 최신 골드 조회 (새로 추가)
        String selectSql = "SELECT gold FROM `PLAYER` WHERE PLAYER_ID = ?";
        // queryForObject를 사용해 단일 정수 값을 가져옵니다.
        return jdbcTemplate.queryForObject(selectSql, Integer.class, playerId);
    }
    
    public int updateMinGold(String playerId, int amount) {
        System.out.println("구매 요청 - ID: " + playerId + ", 필요 금액: " + amount);
        
        String updateSql = "UPDATE `PLAYER` SET gold = gold - ? WHERE PLAYER_ID = ? AND gold >= ?";
        int rowsAffected = jdbcTemplate.update(updateSql, amount, playerId, amount);
        
        if (rowsAffected == 0) {
            System.out.println(">>> [경고] 골드 부족으로 구매 실패!");
            return -1; 
        }
        
        String selectSql = "SELECT gold FROM `PLAYER` WHERE PLAYER_ID = ?";
        return jdbcTemplate.queryForObject(selectSql, Integer.class, playerId);
    }

    // 나무 심기 업데이트
    public void addTree(int playerId) {
        String sql = "UPDATE MAPS SET tree_Count = tree_Count + 1 WHERE map_id = ?";
        jdbcTemplate.update(sql, playerId);
    }
    
    // 플레이어 정보 가져오기
    public Map<String, Object> getDecoDetail(String username) {
        String sql = "SELECT DECO_SCORE FROM `DECO_DETAIL` WHERE PLAYER_ID = ?";
        return jdbcTemplate.queryForMap(sql, username);
    }
}