package com.example.demo.service;

import org.springframework.stereotype.Service;
import org.springframework.web.client.RestTemplate;
import org.springframework.http.ResponseEntity;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

@Service
public class ApiService {

    public String getShortNpcDialog() {
        // 1. 확인하신 실제 Request URL로 수정
        String url = "http://127.0.0.1:8000/ai/npc-dialog";
        
        RestTemplate restTemplate = new RestTemplate();

        // 2. 데이터 구성 (기존 동일)
        Map<String, Object> requestBody = new HashMap<>();
        requestBody.put("userId", 1);
        requestBody.put("npcId", 1);
        
        List<String> items = new ArrayList<>();
        items.add("쓰레기");
        items.add("플라스틱병");
        requestBody.put("items", items);
        
        requestBody.put("currentWeather", "acid_rain");
        requestBody.put("pollutionLevel", 75.0);
        requestBody.put("tradeHistory", new ArrayList<>());

        try {
            // 3. POST 호출
            ResponseEntity<String> response = restTemplate.postForEntity(url, requestBody, String.class);
            return response.getBody();
        } catch (Exception e) {
            return "여전히 폭풍우가 치고 있습니다: " + e.getMessage();
        }
    }
}