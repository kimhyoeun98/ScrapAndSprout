package com.example.demo.controller;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import com.example.demo.service.TradeService;
import com.example.demo.util.JwtUtil;

import java.util.Map;
import java.util.HashMap;
import java.util.List;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;


@RestController
@RequestMapping("/api")
public class TradeController {

    private final TradeService tradeService;
    private final JwtUtil jwtUtil; // 1. 주입 추가

    public TradeController(TradeService tradeService, JwtUtil jwtUtil) {
        this.tradeService = tradeService;
        this.jwtUtil = jwtUtil;
    }

    @PostMapping("/auth/login")
    public Map<String, Object> login(@RequestBody Map<String, String> request) {
        String userName = request.get("user_name");
        String password = request.get("password");

        Map<String, Object> user = tradeService.getUserByUsername(userName, password);
        Map<String, Object> response = new HashMap<>();

        if (user != null) {
            // ✅ PLAYER 테이블 컬럼명 기준
            // PLAYER_ID가 문자열이면 String으로 처리
            String playerId = String.valueOf(user.get("PLAYER_ID"));
            String realToken = jwtUtil.generateToken(playerId.hashCode(), userName);

            response.put("success", true);
            response.put("token", realToken);
            response.put("user_id", playerId);
            response.put("user_name", playerId); // PLAYER_ID가 이름 역할
            response.put("message", "JWT 로그인 성공!");
        } else {
            response.put("success", false);
            response.put("message", "아이디 또는 비밀번호가 틀렸습니다.");
        }
        return response;
    }
    
    @PostMapping("/trade/sell")
    public Map<String, Object> sellItems(@RequestBody Map<String, Object> request) {
        String user_id = request.get("playerId").toString();
        List<String> itemNm = (List<String>) request.get("itemNames");
        List<Integer> counts = (List<Integer>) request.get("itemCounts");
        String ch = (String) request.get("characterType");
        int total_item_cnt = 0;
        int earnGold = 0;
        System.out.println("|||" + itemNm.get(0) + "|||");
        if (counts != null) {
        	int index = 0;
            for (Object count : counts) {
            	if(itemNm.get(index).equals("휴지"))
            		earnGold += 7;
            	if(itemNm.get(index).equals("바나나껍질"))
            		earnGold += 10;
            	if(itemNm.get(index).equals("음료캔"))
            		earnGold += 15;
            	if(itemNm.get(index).equals("디스크"))
            		earnGold += 30;
            	if(itemNm.get(index).equals("타이어"))
            		earnGold += 100;
            	if(itemNm.get(index).equals("드럼통"))
            		earnGold += 200;
            	if(itemNm.get(index).equals("컴퓨터"))
            		earnGold += 350;
            	
                // JSON 숫자는 가끔 Double로 넘어올 수 있어 안전하게 변환합니다.
                total_item_cnt += Integer.parseInt(count.toString());
                index++;
            }
        }

        System.out.println(">>> 판매 총 수량: " + total_item_cnt);
        
        if(ch.equals("Delta")) {
        	earnGold = (int) (earnGold + (earnGold*0.5));
        }
        
        int updatedTotalGold = tradeService.updateGold(user_id, earnGold);
        // 4. 응답 구성
        Map<String, Object> response = new HashMap<>();
        response.put("success", true);
        response.put("gold", updatedTotalGold);
        response.put("message", "쓰레기 " + total_item_cnt + "개 판매 완료!");
        
        return response;
    }

    // 2. 아이템 구매
    @PostMapping("/trade/buy")
    public Map<String, Object> buyItem(@RequestBody Map<String, Object> request) {
        System.out.println(">>> 구매 요청 접수");
        
        String user_id = request.get("playerId").toString();
        String itemName = (String) request.get("itemName");
        String ch = (String) request.get("");
        
        
        int quantity = Integer.parseInt(request.get("quantity").toString());
        
        // 가격은 타입(키의 마지막 단어)으로 결정 — 모든 테마 공통 (클라 DecoCatalog와 동일 규칙)
        int unitPrice = 0;
        String type = (itemName != null && itemName.contains(" "))
                ? itemName.substring(itemName.lastIndexOf(' ') + 1)
                : itemName;
        if (type != null) {
        	if (type != null) {
        	    if (type.contains("나무")) {
        	        unitPrice = 40;
        	    } else if (type.contains("상자")) {
        	        unitPrice = 20;
        	    } else if (type.contains("의자")) {
        	        unitPrice = 30;
        	    } else if (type.contains("울타리")) {
        	        unitPrice = 50;
        	    } else if (type.contains("꽃병")) {
        	        unitPrice = 60;
        	    } else if (type.contains("탁자")) {
        	        unitPrice = 100;
        	    } else if (type.contains("꽃밭")) {
        	        unitPrice = 200;
        	    } else {
        	        unitPrice = 0; // 일치하는 핵심 단어가 없을 때
        	    }
        	}
        }

        // 알 수 없는 아이템 공짜 구매 차단
        if (unitPrice <= 0) {
            Map<String, Object> resp = new HashMap<>();
            resp.put("success", false);
            resp.put("gold", -1);
            resp.put("message", "알 수 없는 아이템: " + itemName);
            return resp;
        }
        
        		
        int totalAmount = unitPrice * quantity;
        
        System.out.println(itemName);
        
        int updatedTotalGold = tradeService.updateMinGold(user_id, totalAmount);
        
        Map<String, Object> response = new HashMap<>();

        if (updatedTotalGold == -1) {
            response.put("success", false);
            response.put("gold", -1); // 혹은 현재 골드를 다시 조회해서 넣어줘도 됨
            response.put("message", "골드가 부족하여 " + itemName + "을(를) 구매할 수 없습니다.");
        } else {
            response.put("success", true);
            response.put("gold", updatedTotalGold); 
            response.put("message", itemName + " " + quantity + "개 구매에 성공했습니다.");
        }
        
        return response;
    }

    // 3. 나무 식재 (위치 정보가 임의의 소수점이어도 받아냄)
    @PostMapping("/plant")
    public Map<String, Object> plantTree(@RequestBody Map<String, Object> request) {
        Object posX = request.get("posX");
        Object posY = request.get("posY");

        System.out.println(">>> 나무 심기 위치: X=" + posX + ", Y=" + posY);

        Map<String, Object> response = new HashMap<>();
        response.put("success", true);
        response.put("treeCount", 10); // 임의의 갱신된 나무 수
        response.put("message", "좌표 " + posX + ", " + posY + " 위치에 나무를 심었습니다.");
        
        return response;
    }

    // 4. 플레이어 정보 조회 (GET 방식이라 URL 접속 가능)
    @GetMapping("/player/{playerId}")
    public Map<String, Object> getPlayerInfo(@PathVariable String playerId) {
        return tradeService.getPlayer(playerId);
    }
    
    // 4-1 . 플레이어 정보 조회 (session)
    @GetMapping("/session/init")
    public Map<String, Object> getPlayerInit(@PathVariable String playerId) {
        return tradeService.getPlayer(playerId);
    }
    
    // 5. 꾸미기 점수 리턴
    @PostMapping("/deco/place")
    public Map<String, Object> decoScore(@RequestBody Map<String, Object> request){
        String playerId = (String) request.get("playerId");
        String itemType = (String) request.get("itemType");
        int score = Integer.parseInt(request.get("score").toString());

        Map<String, Object> response = new HashMap<>();
        response.put("playerId", playerId);
        response.put("itemType", -1); // 혹은 현재 골드를 다시 조회해서 넣어줘도 됨
        response.put("score", tradeService.getDecoDetail(playerId));
        
        return response;
    }
  
    public String postMethodName(@RequestBody String entity) {
        //TODO: process POST request
        
        return entity;
    }
    // [API] 아이디 중복 확인
    @GetMapping("/checkid")
    public ResponseEntity<?> checkId(@RequestParam("username") String username) {
        boolean isDuplicate = tradeService.checkIdDuplicate(username);
        Map<String, Object> response = new HashMap<>();
        
        if (isDuplicate) {
            response.put("available", false);
            response.put("message", "이미 존재하는 아이디입니다.");
            return ResponseEntity.ok(response); // 200 OK지만 사용 불가
        }
        
        response.put("available", true);
        response.put("message", "사용 가능한 아이디입니다.");
        return ResponseEntity.ok(response);
    }

    // [API] 회원가입
    @PostMapping("/signup")
    public ResponseEntity<?> signUp(@RequestBody Map<String, String> request) {
        String id = request.get("user_id");
        String password = request.get("password");
        String name = request.get("user_name");
        String email = request.get("email");

        boolean success = tradeService.registerUser(id, password, name, email);
        Map<String, Object> response = new HashMap<>();

        if (!success) {
            response.put("success", false);
            response.put("message", "회원가입에 실패했습니다. 입력값을 확인해주세요.");
            return ResponseEntity.badRequest().body(response);
        }

        response.put("success", true);
        response.put("message", "회원가입이 완료되었습니다!");
        return ResponseEntity.ok(response);
    }
    
    
}