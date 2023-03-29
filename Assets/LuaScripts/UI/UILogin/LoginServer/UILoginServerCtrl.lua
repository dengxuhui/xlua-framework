--
-- author:dengxuhui
-- create date:2023/3/29
-- 
-- @File-> Assets.LuaScripts.UI.UILogin.LoginServer.UILoginServerCtrl
-- @require-> local UILoginServerCtrl = require "UI.UILogin.LoginServer.UILoginServerCtrl"

local UILoginServerCtrl = BaseClass("UILoginServerCtrl", UIBaseCtrl)

local function SetSelectedServer(self, svr_id)
	-- 合法性校验
	if svr_id == nil then
		-- TODO：错误弹窗
		Logger.LogError("svr_id nil")
		return
	end
	local servers = ServerData:I().servers
	if servers[svr_id] == nil then
		-- TODO：错误弹窗
		Logger.LogError("no svr_id : "..tostring(svr_id))
		return
	end
	ClientData:I():SetLoginServerID(svr_id)
end

local function CloseSelf(self)
	UIManager:I():CloseWindow(UIWindowNames.UILoginServer)
end

UILoginServerCtrl.SetSelectedServer = SetSelectedServer
UILoginServerCtrl.CloseSelf = CloseSelf

return UILoginServerCtrl