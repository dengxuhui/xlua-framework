--
-- author:dengxuhui
-- create date:2023/3/29
-- 
-- @File-> Assets.LuaScripts.UI.UILogin.Login.UILoginCtrl
-- @require-> local UILoginCtrl = require "UI.UILogin.Login.UILoginCtrl"

local UILoginCtrl = BaseClass("UILoginCtrl", UIBaseCtrl)

local function LoginServer(self, name, password)
	-- 合法性检验
	if string.len(name) > 20 or string.len(name) < 1 then
		-- TODO：错误弹窗
		Logger.LogError("name length err!")
	    return;
	end
	if string.len(password) > 20 or string.len(password) < 1 then
		-- TODO：错误弹窗
		Logger.LogError("password length err!")
	    return;
	end
	-- 检测是否有汉字
	for i=1, string.len(name) do
		local curByte = string.byte(name, i)
	    if curByte > 127 then
			-- TODO：错误弹窗
			Logger.LogError("name err : only ascii can be used!")
	        return;
	    end;
	end
	
	ClientData:I():SetAccountInfo(name, password)
	
	SceneManager:I():SwitchScene(SceneConfig.HomeScene)
end

local function ChooseServer(self)
	UIManager:I():OpenWindow(UIWindowNames.UILoginServer)
end

UILoginCtrl.LoginServer = LoginServer
UILoginCtrl.ChooseServer = ChooseServer

return UILoginCtrl