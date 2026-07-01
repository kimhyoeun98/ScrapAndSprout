package com.example.demo.controller;

import com.example.demo.service.AchievementService;
import org.springframework.web.bind.annotation.*;
import java.util.*;

@RestController
@RequestMapping("/api/achievements")
public class AchievementController {

    private final AchievementService achievementService;

    public AchievementController(AchievementService achievementService) {
        System.out.println("[AchievementController] 컨트롤러 로드 완료");
        this.achievementService = achievementService;
    }

    // 1. 업적 목록 조회 (GET)
    // 유니티가 JsonUtility로 파싱할 수 있도록 List를 바로 주지 않고, 
    // 기존 유니티 코드의 wrapper 구조와 맞춰서 그냥 반환합니다.
    // (유니티 코드에서 이미 "{\"achievements\":" + text + "}" 처리를 하고 있으므로 List 그대로 반환해도 되지만, 
    //  타입 안정성을 위해 아래와 같이 안전하게 List를 반환합니다.)
    @GetMapping
    public List<Map<String, Object>> getAchievements(@RequestParam("playerId") String playerId) {
    	System.out.println(">>>> " + playerId);
        return achievementService.getAchievements(playerId);
    }

    // 2. 업적 업데이트 (POST)
    @PostMapping("/update")
    public Map<String, Object> updateAchievement(@RequestBody Map<String, Object> request) {
        String playerId = (String) request.get("playerId");
        String type = (String) request.get("achievementType");
        
        // 데이터 타입을 안전하게 파싱 (Integer 체크)
        int amount = 0;
        if (request.get("progressAmount") != null) {
            amount = ((Number) request.get("progressAmount")).intValue();
        }

        // 서비스에서 비즈니스 로직 수행
        Map<String, Object> result = achievementService.updateAchievementProgress(playerId, type, amount);
        
        // [중요] 유니티의 AchievementUnlockResponse 구조에 맞춰 응답 데이터 가공
        // 유니티 코드의 response.unlockedAchievement 구조를 만족해야 합니다.
        Map<String, Object> response = new HashMap<>();
        
        // 서비스에서 리턴해준 값이 '달성된 업적 정보' 자체라면 이를 매핑해줍니다.
        // 만약 업적이 달성되지 않았다면 빈 값이나 null 처리가 되도록 구조화해야 합니다.
        if (result != null && !result.isEmpty()) {
            response.put("unlockedAchievement", result);
        } else {
            response.put("unlockedAchievement", null);
        }
        
        return response;
    }
}