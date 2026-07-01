package com.example.demo.controller;

import com.example.demo.service.ApiService; // ApiService 위치에 맞게 임포트하세요
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
public class HelloController {

    private final ApiService apiService;

    // 생성자 주입: 스프링이 자동으로 ApiService를 연결해줍니다.
    public HelloController(ApiService apiService) {
        this.apiService = apiService;
    }

    @GetMapping("/hello")
    public String hello() {
        return "Hello, Captain! Spring 5 Server is running!";
    }

    // NPC 대사 API 호출 결과를 확인하는 새로운 경로입니다.
    @GetMapping("/npc/dialog")
    public String getNpcDialog() {
        return apiService.getShortNpcDialog();
    }
}