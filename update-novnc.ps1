# ============================================
# noVNC 更新脚本
# 功能：从GitHub拉取最新的noVNC代码并更新到项目中
# ============================================

# 设置错误处理
$ErrorActionPreference = "Stop"

# 定义颜色输出函数
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Type = "Info"
    )
    
    switch ($Type) {
        "Success" { Write-Host $Message -ForegroundColor Green }
        "Error" { Write-Host $Message -ForegroundColor Red }
        "Warning" { Write-Host $Message -ForegroundColor Yellow }
        "Info" { Write-Host $Message -ForegroundColor Cyan }
        default { Write-Host $Message }
    }
}

# 检查Git是否安装
function Test-GitInstalled {
    try {
        $gitVersion = git --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ 检测到 Git: $gitVersion" "Success"
            return $true
        }
        else {
            Write-ColorOutput "✗ 错误: 未检测到Git，请先安装Git" "Error"
            return $false
        }
    }
    catch {
        Write-ColorOutput "✗ 错误: 未检测到Git，请先安装Git" "Error"
        return $false
    }
}

# 检查网络连接
function Test-NetworkConnection {
    try {
        Write-ColorOutput "检查GitHub连接..." "Info"
        $response = Test-Connection -ComputerName "github.com" -Count 1 -Quiet -ErrorAction SilentlyContinue
        if ($response) {
            Write-ColorOutput "✓ 网络连接正常" "Success"
            return $true
        }
        else {
            Write-ColorOutput "⚠ 警告: 无法连接到GitHub，请检查网络" "Warning"
            return $false
        }
    }
    catch {
        Write-ColorOutput "⚠ 警告: 网络检查失败" "Warning"
        return $false
    }
}

# 主函数
function Update-NoVNC {
    Write-ColorOutput "`n========================================" "Info"
    Write-ColorOutput "开始更新 noVNC" "Info"
    Write-ColorOutput "========================================`n" "Info"

    # 检查Git
    if (-not (Test-GitInstalled)) {
        exit 1
    }

    # 检查网络连接
    Test-NetworkConnection | Out-Null

    # 定义路径
    $rootPath = Get-Location
    $tempPath = Join-Path $rootPath "temp_novnc"
    $srcPath = Join-Path $rootPath "src"
    $wwwrootPath = Join-Path $rootPath "src\noVNCClient\wwwroot"
    $viewsPath = Join-Path $rootPath "src\noVNCClient\Views\Home"
    
    Write-ColorOutput "工作目录: $rootPath" "Info"
    Write-ColorOutput "目标目录: $wwwrootPath`n" "Info"

    # 检查src目录是否存在
    if (-not (Test-Path $srcPath)) {
        Write-ColorOutput "✗ 错误: 未找到src目录，请确保在项目根目录下执行此脚本" "Error"
        exit 1
    }

    try {
        # ============================================
        # 步骤 1: 克隆noVNC仓库
        # ============================================
        Write-ColorOutput "[步骤 1/4] 从GitHub拉取最新代码..." "Info"
        
        # 如果临时目录已存在，先删除
        if (Test-Path $tempPath) {
            Write-ColorOutput "删除旧的临时目录..." "Warning"
            try {
                Remove-Item -Path $tempPath -Recurse -Force -ErrorAction Stop
            }
            catch {
                throw "无法删除临时目录: $($_.Exception.Message)"
            }
        }

        # 克隆noVNC仓库（只克隆master分支，深度为1以加快速度）
        Write-ColorOutput "正在克隆 noVNC 仓库（master分支）..." "Info"
        git clone --branch master --depth 1 https://github.com/novnc/noVNC.git $tempPath 2>&1 | Out-Null
        
        # 检查git命令是否成功
        if ($LASTEXITCODE -ne 0) {
            throw "Git克隆失败，退出码: $LASTEXITCODE"
        }
        
        if (-not (Test-Path $tempPath)) {
            throw "克隆失败：临时目录未创建"
        }
        
        Write-ColorOutput "✓ 代码拉取成功`n" "Success"

        # ============================================
        # 步骤 2: 复制文件到wwwroot
        # ============================================
        Write-ColorOutput "[步骤 2/4] 复制文件到wwwroot目录..." "Info"
        
        # 检查wwwroot目录
        if (-not (Test-Path $wwwrootPath)) {
            Write-ColorOutput "创建wwwroot目录..." "Warning"
            New-Item -ItemType Directory -Path $wwwrootPath -Force | Out-Null
        }

        # 清空wwwroot目录
        Write-ColorOutput "清空wwwroot目录..." "Info"
        $existingItems = Get-ChildItem -Path $wwwrootPath -Force -ErrorAction SilentlyContinue
        if ($existingItems) {
            $existingItems | Remove-Item -Recurse -Force -ErrorAction Stop
        }

        # 复制所有文件（排除.git目录）
        Write-ColorOutput "复制文件（排除.git目录）..." "Info"
        $copiedFiles = 0

        Get-ChildItem -Path $tempPath -Recurse -Force | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } | ForEach-Object {
            $targetPath = $_.FullName.Replace($tempPath, $wwwrootPath)
            
            if ($_.PSIsContainer) {
                if (-not (Test-Path $targetPath)) {
                    New-Item -ItemType Directory -Path $targetPath -Force -ErrorAction Stop | Out-Null
                }
            }
            else {
                # 确保目标目录存在
                $targetDir = Split-Path -Parent $targetPath
                if (-not (Test-Path $targetDir)) {
                    New-Item -ItemType Directory -Path $targetDir -Force -ErrorAction Stop | Out-Null
                }
                Copy-Item -Path $_.FullName -Destination $targetPath -Force -ErrorAction Stop
                $copiedFiles++
            }
        }
        
        Write-ColorOutput "✓ 已复制 $copiedFiles 个文件`n" "Success"

        # ============================================
        # 步骤 3: 修改默认缩放模式为本地缩放
        # ============================================
        Write-ColorOutput "[步骤 3/4] 修改默认缩放模式为本地缩放..." "Info"

        $uiJsPath = Join-Path $wwwrootPath "app\ui.js"
        if (Test-Path $uiJsPath) {
            $uiContent = Get-Content -Path $uiJsPath -Raw -Encoding UTF8
            $originalContent = $uiContent
            $uiContent = $uiContent.Replace("UI.initSetting('resize', 'off')", "UI.initSetting('resize', 'scale')")

            if ($uiContent -ne $originalContent) {
                Set-Content -Path $uiJsPath -Value $uiContent -NoNewline -Encoding UTF8
                Write-ColorOutput "✓ 已将默认缩放模式修改为本地缩放（Local Scaling）`n" "Success"
            }
            else {
                Write-ColorOutput "⚠ 未找到需要替换的缩放设置，可能noVNC版本已变更`n" "Warning"
            }
        }
        else {
            Write-ColorOutput "⚠ 未找到 app/ui.js 文件，跳过缩放模式修改`n" "Warning"
        }

        # ============================================
        # 步骤 4: 清理临时文件
        # ============================================
        Write-ColorOutput "[步骤 4/4] 清理临时文件..." "Info"
        
        if (Test-Path $tempPath) {
            Remove-Item -Path $tempPath -Recurse -Force
            Write-ColorOutput "✓ 临时文件已清理`n" "Success"
        }

        # ============================================
        # 完成
        # ============================================
        Write-ColorOutput "========================================" "Success"
        Write-ColorOutput "✓ noVNC 更新完成！" "Success"
        Write-ColorOutput "========================================`n" "Success"
        
    }
    catch {
        Write-ColorOutput "`n========================================" "Error"
        Write-ColorOutput "✗ 更新失败！" "Error"
        Write-ColorOutput "========================================" "Error"
        Write-ColorOutput "错误信息: $($_.Exception.Message)" "Error"
        Write-ColorOutput "错误位置: $($_.InvocationInfo.ScriptLineNumber):$($_.InvocationInfo.OffsetInLine)`n" "Error"
        
        # 清理临时文件
        if (Test-Path $tempPath) {
            Write-ColorOutput "清理临时文件..." "Warning"
            Remove-Item -Path $tempPath -Recurse -Force -ErrorAction SilentlyContinue
        }
        
        exit 1
    }
}

# 执行主函数
Update-NoVNC

