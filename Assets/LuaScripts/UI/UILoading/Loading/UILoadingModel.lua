--
-- author:dengxuhui
-- create date:2023/3/29
-- 加载界面数据
-- @File-> Assets.LuaScripts.UI.UILoading.Loading.UILoadingModel
-- @require-> local UILoadingModel = require "UI.UILoading.Loading.UILoadingModel"

local UILoadingModel = BaseClass("UILoadingModel", UIBaseModel)
local base = UIBaseModel

-- 打开
local function OnEnable(self)
	base.OnEnable(self)
	-- 进度
	self.value = 0
end

-- 关闭
local function OnDisable(self)
	base.OnDisable(self)
	self.value = 0
end

UILoadingModel.OnEnable = OnEnable
UILoadingModel.OnDisable = OnDisable

return UILoadingModel